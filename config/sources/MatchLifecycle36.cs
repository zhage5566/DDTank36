using System;
using Game.Logic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameRoom.Handle;
using Game.Server.Rooms;
namespace Local36 {
 // Hook only native start/cancel commands; preserve every other room command.
 public static class MatchLifecycle36 {
  static NativeMethodHook36 start,cancel,stop;
  static Action<BaseRoom> StopOriginal;
  static Func<GameStart,GamePlayer,GSPacketIn,bool> StartOriginal;
  static Func<GamePickupCancel,GamePlayer,GSPacketIn,bool> CancelOriginal;
  public static void Install(){if(start!=null)return;var f=BindingFlags.Static|BindingFlags.NonPublic;
   start=new NativeMethodHook36(typeof(GameStart).GetMethod("CommandHandler"),typeof(MatchLifecycle36).GetMethod("Start",f));start.Apply();StartOriginal=start.Original<Func<GameStart,GamePlayer,GSPacketIn,bool>>();
   cancel=new NativeMethodHook36(typeof(GamePickupCancel).GetMethod("CommandHandler"),typeof(MatchLifecycle36).GetMethod("Cancel",f));cancel.Apply();CancelOriginal=cancel.Original<Func<GamePickupCancel,GamePlayer,GSPacketIn,bool>>();
   stop=new NativeMethodHook36(typeof(RoomMgr).GetMethod("StopProxyGame"),typeof(MatchLifecycle36).GetMethod("Stop",f));stop.Apply();StopOriginal=stop.Original<Action<BaseRoom>>();
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static void Stop(BaseRoom room){var game=room.Game;StopOriginal(room);BotService.ProxyStopped(room,game);}
  [MethodImpl(MethodImplOptions.NoInlining)]static bool Start(GameStart handler,GamePlayer player,GSPacketIn packet){bool result=StartOriginal(handler,player,packet);if(result&&!BotService.IsBot(player.PlayerId))BotService.MatchRequested(player);return result;}
  [MethodImpl(MethodImplOptions.NoInlining)]static bool Cancel(GamePickupCancel handler,GamePlayer player,GSPacketIn packet){var room=player.CurrentRoom;
   if(room!=null&&room.RoomType==eRoomType.Match&&room.Host==player&&room.Game==null){BotService.CancelMatchingRoom(room,"player_cancelled");return true;}
   return CancelOriginal(handler,player,packet);
  }
 }
}
