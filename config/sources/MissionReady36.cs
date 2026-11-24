using System;
using System.Collections.Generic;
using System.Linq;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.Rooms;
namespace Local36 {
 // Exact shipped MissionRoomView.prepareGame: 91/116 true, then 94/15 byte1.
 // A between-mission native room stays IsPlaying=true; lobby-only ready checks
 // cannot prepare the next session. Native state and packet handlers remain final.
 public static class MissionReady36 {
  sealed class Cursor {public PVEGame Game;public int Session;public DateTime Sent;}
  static readonly object Gate=new object();static readonly Dictionary<int,Cursor> Sent=new Dictionary<int,Cursor>();
  public static void Tick(BotPlayer bot){var room=bot.CurrentRoom;var game=room==null?null:room.Game as PVEGame;if(game==null||game.GameState!=eGameState.SessionPrepared||!BotService.IsBot(bot.PlayerId))return;
   bool host=room.Host==bot;if(host&&game.GetAllLivingPlayers().Any(p=>p.PlayerDetail.PlayerCharacter.ID!=bot.PlayerId&&!p.Ready))return;
   lock(Gate){Cursor old;if(Sent.TryGetValue(bot.PlayerId,out old)&&Object.ReferenceEquals(old.Game,game)&&old.Session==game.SessionId&&(DateTime.UtcNow-old.Sent).TotalSeconds<5)return;Sent[bot.PlayerId]=new Cursor{Game=game,Session=game.SessionId,Sent=DateTime.UtcNow};}
   if(host){var start=new GSPacketIn(82,bot.PlayerId);start.WriteBoolean(true);start.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(start);Console.WriteLine("BOT MISSION START: "+bot.PlayerId+" room="+room.RoomId+" session="+game.SessionId);return;}
   var prepare=new GSPacketIn(91,bot.PlayerId);prepare.WriteByte(116);prepare.WriteBoolean(true);prepare.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(prepare);
   if(bot.CurrentRoom!=room||room.Game!=game||game.GameState!=eGameState.SessionPrepared)return;
   var state=new GSPacketIn(94,bot.PlayerId);state.WriteInt(15);state.WriteByte(1);state.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(state);
   Console.WriteLine("BOT MISSION READY: "+bot.PlayerId+" room="+room.RoomId+" session="+game.SessionId);
  }
 }
}
