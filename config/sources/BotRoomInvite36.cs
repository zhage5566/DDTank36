using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading;
using Game.Base.Packets;
using Game.Server;
using Game.Server.Packets.Client;
using Game.Server.Rooms;
namespace Local36 {
 public static class BotRoomInvite36 {
  static NativeMethodHook36 Hook;static Func<GameInviteHandler,GameClient,GSPacketIn,int> Original;
  public static void Install(){if(Hook!=null)return;Hook=new NativeMethodHook36(typeof(GameInviteHandler).GetMethod("HandlePacket"),typeof(BotRoomInvite36).GetMethod("Handle",BindingFlags.Static|BindingFlags.NonPublic));Hook.Apply();Original=Hook.Original<Func<GameInviteHandler,GameClient,GSPacketIn,int>>();}
  [MethodImpl(MethodImplOptions.NoInlining)]static int Handle(GameInviteHandler handler,GameClient client,GSPacketIn packet){
   try{var copy=new GSPacketIn((byte[])packet.Buffer.Clone(),packet.Length);copy.Offset=GSPacketIn.HDR_SIZE;int target=copy.ReadInt();
    var room=client.Player==null?null:client.Player.CurrentRoom;
    if(BotService.IsBot(target)&&room!=null){BotService.RequestRoomInvitation(target,client.Player);return 0;}
   }catch(Exception e){Console.WriteLine("BOT INVITE SEAT: "+e.GetType().Name);}
   return Original(handler,client,packet);
  }
 }
}
