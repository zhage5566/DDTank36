// 3.6 presence repair uses native packet writers. No ranking/stat fabrication.
// Client evidence: RoomListController.__addWaitingPlayer; DungeonListController;
// ChatManager.__sceneChat reads the first int as zoneID (AreaID, not ServerID).
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Rooms;

namespace Local36 {
 public static class Presence36 {
  sealed class ViewerState {public GamePlayer Player;public int Hall;public HashSet<int> Shown=new HashSet<int>();public DateTime LastFullSync=DateTime.MinValue;}
  static readonly object Gate=new object();static readonly Dictionary<int,ViewerState> Viewers=new Dictionary<int,ViewerState>();
  public static void RemoveLegacyPlaceholders(){foreach(var player in RoomMgr.WaitingRoom.GetPlayersSafe().Where(p=>p.PlayerId<=0&&p.IsRobot))RoomMgr.ExitWaitingRoom(player);}
  // Call after every SCENE_LOGIN, even when the viewer re-enters the same HallType:
  // the client recreates its lobby list while this viewer object can remain alive.
  public static void ResetViewer(int playerId){lock(Gate)Viewers.Remove(playerId);}
  // Call with the configured single-zone identity, before registering a bot.
  public static void SetLocalZone(GamePlayer player,int areaId,int serverId,string areaName){
   if(player==null)throw new ArgumentNullException("player");
   if(areaId<=0||serverId<=0)throw new ArgumentException("Local zone and server IDs must be positive");
   player.PlayerCharacter.AreaID=areaId;player.PlayerCharacter.AreaName=areaName??"";player.ServerID=serverId;
   // HallStateView and PvP lobby send SCENE_LOGIN(1); dungeon lobby sends (2).
   // Native bot construction does not simulate the human SCENE_LOGIN message.
   if(player.HallType==0)player.HallType=1;
  }
  // Read-only diagnostic: distinguish World online registration from waiting-room
  // membership and HallType filtering before deciding on the synchronization call.
  public static object Describe(IEnumerable<GamePlayer> botPlayers){
   var bots=botPlayers.Where(p=>p!=null).ToArray();var waiting=RoomMgr.WaitingRoom.GetPlayersSafe();var noGame=WorldMgr.GetAllPlayersNoGame();
   var waitingIds=new HashSet<int>(waiting.Select(p=>p.PlayerId));var noGameIds=new HashSet<int>(noGame.Select(p=>p.PlayerId));
   return new{waitingCount=waiting.Length,noGameCount=noGame.Length,bots=bots.Select(p=>new{id=p.PlayerId,name=p.PlayerCharacter.NickName,area=p.AreaID,profileArea=p.PlayerCharacter.AreaID,areaName=p.PlayerCharacter.AreaName,server=p.ServerID,hall=p.HallType,active=p.IsActive,isRobot=p.IsRobot,room=p.CurrentRoom==null?0:p.CurrentRoom.RoomId,inWaiting=waitingIds.Contains(p.PlayerId),inNoGame=noGameIds.Contains(p.PlayerId)}).ToArray()};
  }
  // Optional supplement after the native login/lobby flow. Pass real viewers only.
  // A bot has a single native HallType, whereas players can browse both lobby types.
  // The supplement sends current waiting bots to either type with native serializers.
  // It does not move a player or create rooms; native join/invite rules stay in force.
  public static void Synchronize(IEnumerable<GamePlayer> botPlayers,IEnumerable<GamePlayer> realViewers){
   var bots=botPlayers.Where(p=>p!=null&&p.IsActive&&p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&!p.IsInWorldBossRoom).ToArray();var viewers=realViewers.Where(p=>p!=null&&p.IsActive&&p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&!p.IsInWorldBossRoom&&(p.HallType==1||p.HallType==2)).ToArray();
   lock(Gate){var alive=new HashSet<int>(viewers.Select(p=>p.PlayerId));foreach(int gone in Viewers.Keys.Where(id=>!alive.Contains(id)).ToArray())Viewers.Remove(gone);
    foreach(var viewer in viewers){ViewerState state;if(!Viewers.TryGetValue(viewer.PlayerId,out state)||!Object.ReferenceEquals(state.Player,viewer)||state.Hall!=viewer.HallType){state=new ViewerState{Player=viewer,Hall=viewer.HallType};Viewers[viewer.PlayerId]=state;}
     var local=bots.Where(p=>p.PlayerId!=viewer.PlayerId&&p.AreaID==viewer.AreaID).ToDictionary(p=>p.PlayerId);var now=new HashSet<int>(local.Keys);
     // Re-entering another lobby with the same HallType clears the Flash list.
     // The native client stores these by ID, so a periodic snapshot is idempotent.
     bool full=(DateTime.UtcNow-state.LastFullSync).TotalSeconds>=10;
     foreach(int id in now.Where(x=>full||!state.Shown.Contains(x))){viewer.Out.SendSceneAddPlayer(local[id]);}
     if(full)state.LastFullSync=DateTime.UtcNow;
     // The native writer needs the GamePlayer object. Retain objects for removals.
     foreach(int id in state.Shown.Where(x=>!now.Contains(x)).ToArray()){GamePlayer old;lock(Previous){Previous.TryGetValue(id,out old);}if(old!=null)viewer.Out.SendSceneRemovePlayer(old);}
     state.Shown=now;
    }
    lock(Previous){Previous.Clear();foreach(var p in bots)Previous[p.PlayerId]=p;}
   }
  }
  static readonly Dictionary<int,GamePlayer> Previous=new Dictionary<int,GamePlayer>();
  // Gated QA only. The caller owns the observer and must mark the viewer as a
  // temporary real viewer before calling. No inventory/stat/database changes.
  // receive routes through the actual native PacketProcessor, not a fake reply.
  public static object Verify(GamePlayer viewer,GamePlayer target,Action<GSPacketIn> receive,Action<Action<GSPacketIn>> setObserver){
   if(viewer==null||target==null||receive==null||setObserver==null)throw new ArgumentNullException();
   if(viewer.CurrentRoom!=null||target.CurrentRoom!=null)throw new InvalidOperationException("Use idle test actors");
   int originalHall=viewer.HallType,currentHall=0;object profile=null;string profileError=null;var hall1=new HashSet<int>();var hall2=new HashSet<int>();var gate=new object();
   Action<GSPacketIn> observer=packet=>{try{if(packet.Code==74){object parsed=ReadProfile(packet);lock(gate)profile=parsed;}else if(packet.Code==18){lock(gate){if(currentHall==1)hall1.Add(packet.ClientID);else if(currentHall==2)hall2.Add(packet.ClientID);}}}catch(Exception e){lock(gate)profileError=e.Message;}};
   setObserver(observer);
   try{
    var request=new GSPacketIn(74,viewer.PlayerId);request.WriteBoolean(true);request.WriteInt(target.PlayerId);request.Offset=GSPacketIn.HDR_SIZE;receive(request);Thread.Sleep(200);
    for(int hall=1;hall<=2;hall++){lock(gate)currentHall=hall;ResetViewer(viewer.PlayerId);var scene=new GSPacketIn(16,viewer.PlayerId);scene.WriteInt(hall);scene.Offset=GSPacketIn.HDR_SIZE;receive(scene);Thread.Sleep(2500);}
    lock(gate)return new{viewer=viewer.PlayerId,target=target.PlayerId,expected=new{id=target.PlayerId,name=target.PlayerCharacter.NickName,nimbus=target.PlayerCharacter.Nimbus,style=target.PlayerCharacter.Style},profile=profile,profileError=profileError,hall1=new{received=hall1.Count,targetReceived=hall1.Contains(target.PlayerId),ids=hall1.OrderBy(x=>x).ToArray()},hall2=new{received=hall2.Count,targetReceived=hall2.Contains(target.PlayerId),ids=hall2.OrderBy(x=>x).ToArray()},presence=Describe(new[]{viewer,target})};
   }finally{setObserver(null);ResetViewer(viewer.PlayerId);if(originalHall==1||originalHall==2){var scene=new GSPacketIn(16,viewer.PlayerId);scene.WriteInt(originalHall);scene.Offset=GSPacketIn.HDR_SIZE;receive(scene);}else viewer.HallType=originalHall;}
  }
  // Parses the actual 3.6 client itemEquip schema through Nimbus/FightPower.
  // Avoid native UnCompress(): original versions expose an empty implementation.
  public static object ReadProfile(GSPacketIn packet){
   if(packet.Code!=74)throw new InvalidDataException("Expected profile packet 74");
   byte[] packed=new byte[packet.Length-GSPacketIn.HDR_SIZE];Buffer.BlockCopy(packet.Buffer,GSPacketIn.HDR_SIZE,packed,0,packed.Length);
   if(packed.Length<8||packed[0]!=0x78)throw new InvalidDataException("Expected zlib-compressed profile");
   byte[] raw;using(var source=new MemoryStream(packed,2,packed.Length-6,false))using(var deflate=new DeflateStream(source,CompressionMode.Decompress))using(var output=new MemoryStream()){deflate.CopyTo(output);raw=output.ToArray();}
   var r=new ProfileReader(raw);int id=r.Int();string name=r.String();int agility=r.Int(),attack=r.Int();string colors=r.String(),skin=r.String();int defence=r.Int(),gp=r.Int(),grade=r.Int(),luck=r.Int(),blood=r.Int(),hide=r.Int(),repute=r.Int();bool sex=r.Bool();string style=r.String();r.Int();r.Byte();r.Int();for(int i=0;i<4;i++)r.Int();r.String();for(int i=0;i<3;i++)r.Int();r.Bool();r.Int();r.String();r.String();int nimbus=r.Int(),power=r.Int();
   return new{id=id,name=name,grade=grade,nimbus=nimbus,style=style,power=power,blood=blood,attack=attack,defence=defence,agility=agility,luck=luck,hide=hide,sex=sex,colors=colors,skin=skin,gp=gp,repute=repute,bytes=raw.Length};
  }
  sealed class ProfileReader{readonly byte[] Data;int Offset;public ProfileReader(byte[] data){Data=data;}void Need(int n){if(n<0||Offset+n>Data.Length)throw new EndOfStreamException("Truncated 3.6 profile at "+Offset);}public byte Byte(){Need(1);return Data[Offset++];}public bool Bool(){return Byte()!=0;}public int Int(){Need(4);int v=(Data[Offset]<<24)|(Data[Offset+1]<<16)|(Data[Offset+2]<<8)|Data[Offset+3];Offset+=4;return v;}public string String(){Need(2);int n=(Data[Offset]<<8)|Data[Offset+1];Offset+=2;Need(n);string s=Encoding.UTF8.GetString(Data,Offset,n);Offset+=n;return s.Replace("\0","");}}
 }
}
