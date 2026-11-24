using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Rooms;
using Game.Logic;
namespace Local36 {
 // Local token + testing.flag gated, all-bot-only native integration fixtures.
 public static class BotAcceptance36 {
  public static object Handle(HttpListenerContext ctx,Func<int,BotPlayer> bot,Func<int,bool> roster,Action<GamePlayer,int> open){
   int hostId=Int32.Parse(ctx.Request.QueryString["host"]??"0");if(!roster(hostId))throw new Exception("All-bot fixture required");var host=bot(hostId);var room=host.CurrentRoom;
   if(ctx.Request.Url.AbsolutePath=="/test-room-state")return Describe(host);
   if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");
   string action=ctx.Request.QueryString["action"]??"create";
   if(action=="create"){
    if(!SceneInteraction36.IsIdle(host))throw new Exception("Fixture host busy");var kind=(eRoomType)Enum.Parse(typeof(eRoomType),ctx.Request.QueryString["type"]??"Freedom");
    if(kind!=eRoomType.Freedom&&kind!=eRoomType.Dungeon&&kind!=eRoomType.Match)throw new Exception("Unsupported fixture room");
    RoomMgr.CreateRoom(host,"原生流程验收","",kind,20);for(int i=0;i<30&&host.CurrentRoom==null;i++)Thread.Sleep(100);room=host.CurrentRoom;if(room==null)throw new Exception("Creation pending");
    if(kind==eRoomType.Dungeon){room.MapId=1;room.HardLevel=eHardLevel.Simple;room.LevelLimits=0;room.UpdateRoomGameType();}return Describe(host);
   }
   if(room==null||room.Host!=host||room.GetPlayers().Any(p=>!roster(p.PlayerId)))throw new Exception("Only an all-bot fixture host can control its own room");
   if(action=="leave"){foreach(var member in room.GetPlayers().ToArray())RoomMgr.ExitRoom(room,member);return new{ok=true};}
   if(action=="start"){foreach(var member in room.GetPlayers())RoomMgr.UpdatePlayerState(member,1);Thread.Sleep(200);RoomMgr.StartGame(room);return new{ok=true};}
   if(action=="native-start"){foreach(var member in room.GetPlayers())RoomMgr.UpdatePlayerState(member,1);Thread.Sleep(200);var start=new GSPacketIn(94,hostId);start.WriteInt(7);start.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(start);Thread.Sleep(400);return Describe(host);}
   if(action=="open"){open(host,4);return Describe(host);}
   if(action=="cancel"){var stop=new GSPacketIn(94,hostId);stop.WriteInt(11);stop.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(stop);Thread.Sleep(400);return Describe(host);}
   int memberId=Int32.Parse(ctx.Request.QueryString["member"]??"0");if(!roster(memberId)||memberId==hostId)throw new Exception("Distinct fixture member required");var guest=bot(memberId);GSPacketIn packet;
   if(action=="invite"){packet=new GSPacketIn(70,hostId);packet.WriteInt(memberId);}
   else if(action=="kick"){
    if(room.IsPlaying||guest.CurrentRoom!=room)throw new Exception("Kick requires present lobby member");
    var seats=(GamePlayer[])typeof(BaseRoom).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(GamePlayer[])).GetValue(room);int seat=Array.IndexOf(seats,guest);if(seat<0||seat>7)throw new Exception("No native player seat");
    RoomMgr.KickPlayer(room,(byte)seat);return new{ok=true};
   }else if(action=="close"){
    if(room.IsPlaying)throw new Exception("Lobby required");RoomMgr.UpdateRoomPos(room,Int32.Parse(ctx.Request.QueryString["seat"]??"1"),false,0,-100);return new{ok=true};
   }else throw new Exception("Unknown native fixture action");
   packet.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(packet);return new{ok=true};
  }
  static object Describe(BotPlayer host){var room=host.CurrentRoom;var game=room==null?null:room.Game as BaseGame;
   return new{id=host.PlayerId,room=room==null?0:room.RoomId,kind=room==null?null:room.RoomType.ToString(),playing=room!=null&&room.IsPlaying,capacity=room==null?0:room.PlacesCount,canAdd=room!=null&&room.CanAddPlayer(),members=room==null?new int[0]:room.GetPlayers().Select(p=>p.PlayerId).ToArray(),memberStates=room==null?null:room.GetPlayers().Select(p=>new{id=p.PlayerId,kickProtect=p.KickProtect}).ToArray(),game=game==null?null:game.GetType().Name,state=game==null?null:game.GameState.ToString(),turn=game==null?0:game.TurnIndex,session=game is PVEGame?((PVEGame)game).SessionId:0,actors=game==null?null:game.GetAllPlayers().Select(p=>new{id=p.PlayerDetail.PlayerCharacter.ID,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team,attacking=p.IsAttacking,ready=p.Ready,x=p.X,y=p.Y,energy=p.Energy,expectedEnergy=ReferenceRules34.Energy(p.PlayerDetail.PlayerCharacter.Agility),agility=p.PlayerDetail.PlayerCharacter.Agility,hurt=p.TotalHurt,canTake=p.CanTakeOut,cardFinished=p.FinishTakeCard}).ToArray()};
  }
 }
}
