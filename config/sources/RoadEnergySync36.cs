using System;
using System.Reflection;
using Game.Logic.Phy.Object;
using System.Runtime.CompilerServices;
namespace Local36 {
 // Local PvE must use the existing ReferenceRules34.Energy already used by
 // authoritative panel packets and Fight.Local. No new formula is introduced.
 public static class RoadEnergySync36 {
  static NativeMethodHook36 reset,prepare;static Action<Player> ResetOriginal,PrepareOriginal;
  public static void Install(){var flags=BindingFlags.Static|BindingFlags.NonPublic;
   reset=new NativeMethodHook36(typeof(Player).GetMethod("Reset",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly),typeof(RoadEnergySync36).GetMethod("Reset",flags));reset.Apply();ResetOriginal=reset.Original<Action<Player>>();
   prepare=new NativeMethodHook36(typeof(Player).GetMethod("PrepareNewTurn",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly),typeof(RoadEnergySync36).GetMethod("Prepare",flags));prepare.Apply();PrepareOriginal=prepare.Original<Action<Player>>();
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static void Reset(Player actor){ResetOriginal(actor);Sync(actor);}
  [MethodImpl(MethodImplOptions.NoInlining)]static void Prepare(Player actor){PrepareOriginal(actor);Sync(actor);}
  static void Sync(Player __instance){var detail=__instance.PlayerDetail;if(detail==null||detail.PlayerCharacter==null)return;int value=ReferenceRules34.Energy(detail.PlayerCharacter.Agility);var p=typeof(Player).GetProperty("Energy",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null&&p.CanWrite){p.SetValue(__instance,value,null);return;}for(var t=typeof(Player);t!=null;t=t.BaseType){var f=t.GetField("m_energy",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(__instance,value);return;}}throw new InvalidOperationException("Native energy field missing");}
 }
}
