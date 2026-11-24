using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Managers;
using SqlDataProvider.Data;
namespace Local36 {
 // Backward-compatible tail on the existing property packet. The 3.6 client
 // otherwise recomputes damage/armor with different rules from its equipment.
 public static class DerivedProfile34 {
  public const int Marker=0x44333431;
  delegate GSPacketIn ViewCall(AbstractPacketLib output,PlayerInfo info,PlayerProInventory properties);
  static ViewCall OriginalView;static Action<AbstractPacketLib,GSPacketIn> OriginalSend;
  static readonly List<NativeMethodHook36> Hooks=new List<NativeMethodHook36>();
  static readonly FieldInfo Owner=typeof(PlayerProInventory).GetField("m_player",BindingFlags.Instance|BindingFlags.NonPublic);
  [ThreadStatic]static GamePlayer Viewed;
  static bool Installed;
  public static void Install(){if(Installed)return;try{
   var flags=BindingFlags.Static|BindingFlags.NonPublic;
   var send=new NativeMethodHook36(typeof(AbstractPacketLib).GetMethod("SendTCP",new[]{typeof(GSPacketIn)}),typeof(DerivedProfile34).GetMethod("Send",flags));Hooks.Add(send);send.Apply();OriginalSend=send.Original<Action<AbstractPacketLib,GSPacketIn>>();
   var view=new NativeMethodHook36(typeof(AbstractPacketLib).GetMethod("SendUpdatePlayerProperty"),typeof(DerivedProfile34).GetMethod("View",flags));Hooks.Add(view);view.Apply();OriginalView=view.Original<ViewCall>();
   Installed=true;Console.WriteLine("STRICT 3.4 authoritative profile packet extension installed");
  }catch{foreach(var hook in Hooks)hook.Dispose();Hooks.Clear();throw;}}
  [MethodImpl(MethodImplOptions.NoInlining)]static GSPacketIn View(AbstractPacketLib output,PlayerInfo info,PlayerProInventory properties){
   var previous=Viewed;var target=WorldMgr.GetPlayerById(info.ID);if(target==null&&Owner!=null){var owner=Owner.GetValue(properties) as GamePlayer;if(owner!=null&&owner.PlayerId==info.ID)target=owner;}
   Viewed=target;try{return OriginalView(output,info,properties);}finally{Viewed=previous;}
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static void Send(AbstractPacketLib output,GSPacketIn packet){
   var target=Viewed;
   if(StrictDerived34.Ready(target)&&packet.Code==165&&packet.Length>=GSPacketIn.HDR_SIZE+32){int offset=packet.Offset,id;try{packet.Offset=GSPacketIn.HDR_SIZE;id=packet.ReadInt();}finally{packet.Offset=offset;}
    if(id==target.PlayerId){lock(target){packet.Offset=packet.Length;packet.WriteInt(Marker);packet.WriteDouble(target.GetBaseAttack());packet.WriteDouble(target.GetBaseDefence());packet.WriteInt(target.PlayerCharacter.Blood);packet.WriteInt(ReferenceRules34.Energy(target.PlayerCharacter.Agility));packet.WriteInt(target.PlayerCharacter.FightPower);}}
   }
   OriginalSend(output,packet);
  }
 }
}
