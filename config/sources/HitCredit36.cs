using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Logic;
using Game.Logic.Actions;
using Game.Logic.Phy.Object;
namespace Local36 {
 // Restore missing hit-credit events after a confirmed HP loss. This module
 // never changes HP, attack/defence, damage arguments, critical chance or power.
 public static class HitCredit36 {
  static readonly object Gate=new object();static bool installed;
  delegate bool Damage(Living actor,Living source,ref int damageAmount,ref int criticalAmount,string msg);
  static NativeMethodHook36 livingHook,playerHook,bossHook;static Damage LivingOriginal,PlayerOriginal,BossOriginal;
  [ThreadStatic]static int depth;
  sealed class Ledger{public int Expected;}
  public sealed class Before{public int Blood;public Living Source;}
  static readonly ConditionalWeakTable<Living,Ledger> Credits=new ConditionalWeakTable<Living,Ledger>();
  public static void Install(){lock(Gate){if(installed)return;
   livingHook=Hook(typeof(Living),"LivingDamage");LivingOriginal=livingHook.Original<Damage>();
   playerHook=Hook(typeof(Player),"PlayerDamage");PlayerOriginal=playerHook.Original<Damage>();
   bossHook=Hook(typeof(SimpleBoss),"BossDamage");BossOriginal=bossHook.Original<Damage>();
   installed=true;Console.WriteLine("Native hit-credit observer installed; damage formulas unchanged");
  }}
  static NativeMethodHook36 Hook(Type type,string name){var method=type.GetMethod("TakeDamage",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly,null,new[]{typeof(Living),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(string)},null);var hook=new NativeMethodHook36(method,typeof(HitCredit36).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));hook.Apply();return hook;}
  [MethodImpl(MethodImplOptions.NoInlining)]static bool LivingDamage(Living actor,Living source,ref int amount,ref int critical,string msg){return Track(LivingOriginal,actor,source,ref amount,ref critical,msg);}
  [MethodImpl(MethodImplOptions.NoInlining)]static bool PlayerDamage(Living actor,Living source,ref int amount,ref int critical,string msg){return Track(PlayerOriginal,actor,source,ref amount,ref critical,msg);}
  [MethodImpl(MethodImplOptions.NoInlining)]static bool BossDamage(Living actor,Living source,ref int amount,ref int critical,string msg){return Track(BossOriginal,actor,source,ref amount,ref critical,msg);}
  static bool Track(Damage original,Living actor,Living source,ref int amount,ref int critical,string msg){bool outer=depth++==0;var before=outer?new Before{Blood=actor.Blood,Source=source}:null;bool result;try{result=original(actor,source,ref amount,ref critical,msg);}finally{depth--;}if(outer)AfterDamage(actor,amount,critical,result,before);return result;}
  static void AfterDamage(Living __instance,int damageAmount,int criticalAmount,bool __result,Before __state){
   if(__state==null||!__result||__instance.Blood>=__state.Blood||__state.Source==null||__state.Source.Team==__instance.Team||damageAmount+criticalAmount<=0)return;
   var source=__state.Source;var game=source.Game;if(game==null)return;var ledger=Credits.GetOrCreateValue(source);int expected;
   lock(ledger){ledger.Expected=Math.Max(ledger.Expected,source.TotalHurt)+damageAmount+criticalAmount;expected=ledger.Expected;}
   // BombImp may still call its native credit callback after TakeDamage returns.
   // Recheck on the game queue, preserving that callback and avoiding duplicates.
   game.AddAction(new Credit(source,__instance,damageAmount,criticalAmount,expected));
  }
  sealed class Credit:BaseAction{readonly Living source,target;readonly int damage,critical,expected;
   public Credit(Living actor,Living victim,int amount,int extra,int total):base(0,0){source=actor;target=victim;damage=amount;critical=extra;expected=total;}
   protected override void ExecuteImp(BaseGame game,long tick){try{if(source.Game!=game||source.TotalHurt>=expected)return;
    int before=source.TotalHurt;source.OnAfterKillingLiving(target,damage,critical);
    if(source.TotalHurt==before){source.CurrentIsHitTarget=true;source.TotalHurt+=damage+critical;if(!target.IsLiving)source.TotalKill++;game.CurrentTurnTotalDamage=damage+critical;game.TotalHurt+=damage+critical;}
    Console.WriteLine("NATIVE HIT CREDIT RESTORED: game="+game.Id+" source="+source.Id+" target="+target.Id+" hurt="+before+"->"+source.TotalHurt);
   }catch(Exception e){Console.WriteLine("NATIVE HIT CREDIT ERROR: "+e.GetType().Name);}finally{Finish(tick);}}
  }
 }
}
