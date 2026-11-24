using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Logic.Phy.Object;
namespace Local36 {
 public static class CombatDamage34 {
  static readonly List<NativeMethodHook36> Hooks=new List<NativeMethodHook36>();static bool Installed;
  static readonly PropertyInfo Owner=typeof(SimpleBomb).GetProperty("Owner",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);static readonly FieldInfo Radius=typeof(SimpleBomb).GetField("m_radius",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
  static readonly Random Random=new Random();
 // ---- native_damage audit (gated by config/testing.flag). Observes the rebalanced
 // PvP/PvE damage actually dealt/received so each system's boost can be verified. ----
 static string AuditRoot;static bool AuditOn;static DateTime AuditChecked;static readonly object AuditGate=new object();
 public static void Configure(string root){AuditRoot=root;}
 static bool Auditing(){if(AuditRoot==null)return false;if(!AuditOn||(DateTime.UtcNow-AuditChecked).TotalSeconds>10){AuditChecked=DateTime.UtcNow;AuditOn=System.IO.File.Exists(System.IO.Path.Combine(AuditRoot,"config","testing.flag"));}return AuditOn;}
 static string Tag(Living x){return x==null?"?":x is Player?"player":x is SimpleNpc?"npc":x is SimpleBoss?"boss":x.GetType().Name;}
 static void Audit(string kind,Living actor,Living target,double raw,int final){if(!Auditing())return;try{lock(AuditGate)System.IO.File.AppendAllText(System.IO.Path.Combine(AuditRoot,"logs","native-damage-"+System.Diagnostics.Process.GetCurrentProcess().Id+".jsonl"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{eventName="native_damage",utc=DateTime.UtcNow.ToString("o"),kind,actor=Tag(actor),actorName=actor==null?"":actor.Name,actorTeam=actor==null?0:actor.Team,target=Tag(target),targetName=target==null?"":target.Name,targetTeam=target==null?0:target.Team,actorBaseDamage=actor==null?0:actor.BaseDamage,actorAttack=actor==null?0:actor.Attack,actorLucky=actor==null?0:actor.Lucky,actorGrade=actor==null?0:actor.Grade,targetBaseGuard=target==null?0:target.BaseGuard,targetDefence=target==null?0:target.Defence,targetLucky=target==null?0:target.Lucky,targetGrade=target==null?0:target.Grade,damagePlus=actor==null?0:actor.CurrentDamagePlus,shootMinus=actor==null?0:actor.CurrentShootMinus,raw=raw,final=final})+System.Environment.NewLine);}catch{}}
  public static void Install(){if(Installed)return;if(Owner==null||Radius==null)throw new InvalidOperationException("Verified native bomb owner/radius are required");try{Hook(typeof(Living),"MakeDamage","Direct");Hook(typeof(SimpleBomb),"MakeDamage","Bomb");Hook(typeof(Living),"MakeCriticalDamage","Critical");Installed=true;Console.WriteLine("STRICT 3.4 damage/critical rules installed");}catch{foreach(var h in Hooks)h.Dispose();Hooks.Clear();throw;}}
  static void Hook(Type type,string name,string replacement){var source=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);var hook=new NativeMethodHook36(source,typeof(CombatDamage34).GetMethod(replacement,BindingFlags.Static|BindingFlags.NonPublic));Hooks.Add(hook);hook.Apply();}
  static double Raw(Living actor,Living target){return ReferenceRules34.RawDamage(actor.BaseDamage,actor.Attack,actor.Grade,target.BaseGuard,target.Defence,actor.Lucky,actor.CurrentDamagePlus,actor.CurrentShootMinus);}
  [MethodImpl(MethodImplOptions.NoInlining)]static int Direct(Living actor,Living target){double raw=Raw(actor,target);int final=ReferenceRules34.DirectDamage(raw);Audit("direct",actor,target,raw,final);return final;}
  [MethodImpl(MethodImplOptions.NoInlining)]static int Bomb(SimpleBomb bomb,Living target){var actor=(Living)Owner.GetValue(bomb,null);double distance=target.Distance(new Point(bomb.X,bomb.Y));double raw=Raw(actor,target);int final=ReferenceRules34.BombDamage(raw,distance,(int)Radius.GetValue(bomb));Audit("bomb",actor,target,raw,final);return final;}
  [MethodImpl(MethodImplOptions.NoInlining)]static int Critical(Living actor,Living target,int damage){int roll;lock(Random)roll=Random.Next(100);int extra=ReferenceRules34.CriticalExtra(actor.Lucky,damage,roll);Audit("critical",actor,target,damage,extra);return extra;}
  [MethodImpl(MethodImplOptions.NoInlining)]static int BombCritical(SimpleBomb bomb,Living target,int damage){return Critical((Living)Owner.GetValue(bomb,null),target,damage);}
 }
}
