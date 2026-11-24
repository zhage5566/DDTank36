using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Game.Logic.Phy.Object;

namespace Local36 {
 // The shipped 3.6-Frank-CN build compiles Living.TakeDamage / AddBlood / Die
 // (and the Player / SimpleBoss overrides) to no-op stubs — no entity can lose
 // blood, heal, or die. Harmony prefixes returning false bypass those stubs and
 // reapply the reference (DDTank-II-Squad) combat semantics directly against the
 // real m_blood / m_maxBlood / m_isLiving fields.
 public static class TakeDamageFix36 {
  static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  static readonly FieldInfo fBlood=typeof(Living).GetField("m_blood",F);
  static readonly FieldInfo fMaxBlood=typeof(Living).GetField("m_maxBlood",F);
  static readonly FieldInfo fLiving=typeof(Living).GetField("m_isLiving",F);
  static readonly FieldInfo fGame=typeof(Living).GetField("m_game",F);
  static readonly PropertyInfo pIsFrost=typeof(Living).GetProperty("IsFrost",F);
  static readonly MethodInfo mStopAttacking=typeof(Living).GetMethod("StopAttacking",F);
  static readonly MethodInfo mOnDied=typeof(Living).GetMethod("OnDied",F);
  // AddDander/SetDander are declared on TurnedLiving (Player/SimpleBoss derive from it),
  // not on Living — typeof(Living).GetMethod would return null here.
  static readonly MethodInfo mAddDander=typeof(TurnedLiving).GetMethod("AddDander",F);
  static readonly MethodInfo mSetDander=typeof(TurnedLiving).GetMethod("SetDander",F);

  static int Blood(Living x){return (int)fBlood.GetValue(x);}
  static void SetBlood(Living x,int v){fBlood.SetValue(x,v);}
  static bool IsLiving(Living x){return fLiving!=null&&(bool)fLiving.GetValue(x);}
  static bool IsFrozen(Living x){try{return pIsFrost!=null&&(bool)pIsFrost.GetValue(x,null);}catch{return false;}}
  // Dander (怒气) is stored via the Dander property getter/setter, which the runtime
  // VM protection keeps working (unlike the AddDander/SetDander stubs). Read/write it
  // through the property rather than the obfuscated backing field.
  static int GetDander(Living x){try{if(x==null)return 0;var p=x.GetType().GetProperty("Dander",F);return p!=null?(int)p.GetValue(x,null):0;}catch{return 0;}}
  static void SetDander(Living x,int v){try{if(x==null)return;var p=x.GetType().GetProperty("Dander",F);if(p!=null)p.SetValue(x,v,null);}catch{}}
  static bool SyncAtTime(Living x){try{var f=typeof(Living).GetField("m_syncAtTime",F);return f!=null&&(bool)f.GetValue(x);}catch{return false;}}
  static void SyncDander(Living x){try{object game=fGame.GetValue(x);if(game==null)return;var m=game.GetType().GetMethod("SendGameUpdateDander",F);if(m!=null)m.Invoke(game,new object[]{x});}catch{}}

  static void SyncHealth(Living x,int change){
   try{object game=fGame.GetValue(x);if(game==null)return;var m=game.GetType().GetMethod("SendGameUpdateHealth",F);if(m!=null)m.Invoke(game,new object[]{x,6,change});}catch{}
  }

  static bool Apply(Living self,Living source,ref int damageAmount,ref int criticalAmount){
   bool result=false;
   int total=damageAmount+criticalAmount;
   if(!IsFrozen(self)&&Blood(self)>0){
    SetBlood(self,Blood(self)-total);
    SyncHealth(self,total);
    if(Blood(self)<=0)LivingDie(self);
    result=true;
   }
   return result;
  }

  static void LivingDie(Living self){
   try{
    if(Blood(self)>0){SetBlood(self,0);SyncHealth(self,0);}
    if(IsLiving(self)){
     if(mStopAttacking!=null)try{mStopAttacking.Invoke(self,null);}catch{}
     fLiving.SetValue(self,false);
     if(mOnDied!=null)try{mOnDied.Invoke(self,null);}catch{}
    }
   }catch{}
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool LivingPrefix(Living __instance,Living source,ref int damageAmount,ref int criticalAmount,string msg,ref bool __result){
   __result=Apply(__instance,source,ref damageAmount,ref criticalAmount);return false;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool PlayerPrefix(Player __instance,Living source,ref int damageAmount,ref int criticalAmount,string msg,ref bool __result){
   // Reference caps friendly/self damage at one below current blood (no self-kill).
   if((source==__instance||(source!=null&&source.Team==__instance.Team))&&damageAmount+criticalAmount>=Blood(__instance)){
    damageAmount=Blood(__instance)-1;criticalAmount=0;
   }
   __result=Apply(__instance,source,ref damageAmount,ref criticalAmount);
   if(IsLiving(__instance)&&mAddDander!=null)try{mAddDander.Invoke(__instance,new object[]{(damageAmount*2/5+5)/2});}catch{}
   return false;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool BossPrefix(SimpleBoss __instance,Living source,ref int damageAmount,ref int criticalAmount,string msg,ref bool __result){
   __result=Apply(__instance,source,ref damageAmount,ref criticalAmount);return false;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool AddBloodPrefix(Living __instance,int value,ref int __result){
   int cur=Blood(__instance);int max=fMaxBlood!=null?(int)fMaxBlood.GetValue(__instance):cur;
   cur+=value;if(cur>max)cur=max;SetBlood(__instance,cur);
   try{object game=fGame.GetValue(__instance);if(game!=null){var m=game.GetType().GetMethod("SendGameUpdateHealth",F);if(m!=null)m.Invoke(game,new object[]{__instance,0,value});}}catch{}
   __result=value;return false;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool DiePrefix(Living __instance){LivingDie(__instance);return false;}

  // Reference TurnedLiving.AddDander: gain rage when hit (and on some events), clamped
  // to 200 via SetDander. The shipped build compiles both to no-op stubs, so reapply
  // the semantics against the working Dander property.
  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool AddDanderPrefix(TurnedLiving __instance,int value){
   if(value>0&&IsLiving(__instance)){
    int next=GetDander(__instance)+value;if(next>200)next=200;
    SetDander(__instance,next);
    if(SyncAtTime(__instance))SyncDander(__instance);
   }
   return false;
  }
  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool SetDanderPrefix(TurnedLiving __instance,int value){
   int v=value;if(v>200)v=200;
   SetDander(__instance,v);
   if(SyncAtTime(__instance))SyncDander(__instance);
   return false;
  }

  static void Patch(Harmony harmony,Type type,string name,string prefix,Type[] paramTypes){
   MethodInfo source=type.GetMethod(name,F,null,paramTypes,null);
   if(source==null){Console.WriteLine("TAKEDAMAGE FIX: "+type.Name+"."+name+" not found");return;}
   harmony.Patch(source,prefix:new HarmonyMethod(typeof(TakeDamageFix36).GetMethod(prefix,BindingFlags.Static|BindingFlags.NonPublic)));
  }

  public static void Install(Harmony harmony){
   Patch(harmony,typeof(Living),"TakeDamage","LivingPrefix",new[]{typeof(Living),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(string)});
   Patch(harmony,typeof(Player),"TakeDamage","PlayerPrefix",new[]{typeof(Living),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(string)});
   Patch(harmony,typeof(SimpleBoss),"TakeDamage","BossPrefix",new[]{typeof(Living),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(string)});
   Patch(harmony,typeof(Living),"AddBlood","AddBloodPrefix",new[]{typeof(int)});
   Patch(harmony,typeof(Living),"Die","DiePrefix",Type.EmptyTypes);
   Patch(harmony,typeof(TurnedLiving),"AddDander","AddDanderPrefix",new[]{typeof(int)});
   Patch(harmony,typeof(TurnedLiving),"SetDander","SetDanderPrefix",new[]{typeof(int)});
   Console.WriteLine("TAKEDAMAGE FIX installed: Living/Player/SimpleBoss TakeDamage + AddBlood + Die + TurnedLiving AddDander/SetDander reimplemented (Harmony)");
  }

  // --- temporary probe: verify the reimplemented AddDander/SetDander against the
  // working Dander property. Runs once per process on the first attacker. ---
  static bool probed;
  public static void ProbeDander(Player p){
   if(probed)return;probed=true;
   try{
    int get0=GetDander(p);
    if(mAddDander!=null)mAddDander.Invoke(p,new object[]{30});
    int get1=GetDander(p);
    if(mSetDander!=null)mSetDander.Invoke(p,new object[]{200});
    int get2=GetDander(p);
    if(mSetDander!=null)mSetDander.Invoke(p,new object[]{999});
    int get3=GetDander(p);
    SetDander(p,get0);
    Console.WriteLine("DANDER PROBE: get0="+get0+" afterAdd(30)="+get1+" afterSet(200)="+get2+" afterSet(999)="+get3+" mAddDander="+(mAddDander!=null)+" mSetDander="+(mSetDander!=null));
   }catch(Exception e){Console.WriteLine("DANDER PROBE ERROR "+e);}
  }
 }
}
