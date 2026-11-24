using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using HarmonyLib;
using Game.Logic.Phy.Object;

namespace Local36 {
 // Observe-only prefix on Living.TakeDamage — the single chokepoint through which
 // every point of damage is applied, whatever the attack path:
 //   SimpleNpc melee  -> Living.Beat -> Living.MakeDamage -> LivingBeatAction -> TakeDamage
 //   SimpleBoss range -> LivingRangeAttackingAction -> TakeDamage("范围攻击")
 //   player bombs     -> SimpleBomb.MakeDamage -> TakeDamage
 // Player.TakeDamage and SimpleBoss.TakeDamage both call base.TakeDamage, so this
 // prefix still observes them. It never skips or rewrites a value: the rebalanced
 // armour/defence is verified on the "damage received" side by comparing the final
 // damage against the source/target stats recorded here.
 public static class DamageReceiveAudit36 {
  static string Root; static readonly object Gate=new object();
  static bool Enabled(){ return Root!=null && File.Exists(Path.Combine(Root,"config","testing.flag")); }
  static string Tag(Living x){ return x==null?"?":x is Player?"player":x is SimpleNpc?"npc":x is SimpleBoss?"boss":x.GetType().Name; }
  static object Prop(Living x,string name){try{var p=x.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);return p==null?null:p.GetValue(x,null);}catch{return null;}}
  static void BeforeTakeDamage(Living __instance,Living source,ref int damageAmount,ref int criticalAmount,string msg){
   if(!Enabled())return;
   try{
    lock(Gate)File.AppendAllText(Path.Combine(Root,"logs","native-receive-"+System.Diagnostics.Process.GetCurrentProcess().Id+".jsonl"),
     new JavaScriptSerializer().Serialize(new{
      eventName="native_receive",utc=DateTime.UtcNow.ToString("o"),
      target=Tag(__instance),targetName=__instance==null?"":__instance.Name,targetTeam=__instance==null?0:__instance.Team,
      targetBlood=__instance==null?0:__instance.Blood,targetMaxBlood=__instance==null?0:__instance.MaxBlood,
      targetBaseGuard=__instance==null?0:__instance.BaseGuard,targetDefence=__instance==null?0:__instance.Defence,
      targetLucky=__instance==null?0:__instance.Lucky,targetGrade=__instance==null?0:__instance.Grade,
      source=Tag(source),sourceName=source==null?"":source.Name,
      sourceBaseDamage=source==null?0:source.BaseDamage,sourceAttack=source==null?0:source.Attack,
      sourceLucky=source==null?0:source.Lucky,sourceGrade=source==null?0:source.Grade,
      damage=damageAmount,critical=criticalAmount,total=damageAmount+criticalAmount,msg=msg
     })+Environment.NewLine);
   }catch(Exception e){Console.WriteLine("DAMAGE RECEIVE AUDIT: "+e.GetType().Name);}
  }
  static void AfterTakeDamage(Living __instance){
   if(!Enabled())return;
   try{
    lock(Gate)File.AppendAllText(Path.Combine(Root,"logs","native-receive-"+System.Diagnostics.Process.GetCurrentProcess().Id+".jsonl"),
     new JavaScriptSerializer().Serialize(new{
      eventName="native_after",utc=DateTime.UtcNow.ToString("o"),
      target=Tag(__instance),targetName=__instance==null?"":__instance.Name,
      targetBloodAfter=__instance==null?0:__instance.Blood,targetMaxBlood=__instance==null?0:__instance.MaxBlood,
      isLiving=__instance==null?false:__instance.IsLiving,
      isFrost=__instance==null?null:Prop(__instance,"IsFrost"),noTakeDamage=__instance==null?null:Prop(__instance,"NoTakeDamage")
     })+Environment.NewLine);
   }catch(Exception e){Console.WriteLine("DAMAGE RECEIVE AUDIT AFTER: "+e.GetType().Name);}
  }
  public static void Install(Harmony harmony,Assembly core,string root){
   Root=root;
   var living=core.GetType("Game.Logic.Phy.Object.Living",true);
   var takeDamage=living.GetMethod("TakeDamage",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{living,typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(string)},null);
   if(takeDamage==null){Console.WriteLine("DAMAGE RECEIVE AUDIT: TakeDamage not found");return;}
   harmony.Patch(takeDamage,prefix:new HarmonyMethod(typeof(DamageReceiveAudit36).GetMethod("BeforeTakeDamage",BindingFlags.NonPublic|BindingFlags.Static)),postfix:new HarmonyMethod(typeof(DamageReceiveAudit36).GetMethod("AfterTakeDamage",BindingFlags.NonPublic|BindingFlags.Static)));
   Console.WriteLine("DAMAGE RECEIVE AUDIT installed: Living.TakeDamage");
  }
 }
}
