using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.SpaRooms;
using Game.Server.SceneMarryRooms;
using Game.Server.Rooms;
using SqlDataProvider.Data;

namespace Local36 {
 public static class SceneInteraction36 {
  static readonly ConcurrentDictionary<int,bool> Joining=new ConcurrentDictionary<int,bool>();
  public static Action<GamePlayer,GamePlayer,string> InvitationAccepted;
  // Native outgoing packets may not have serialized their header into Buffer yet.
  static GSPacketIn CopyPacket(GSPacketIn original){var copy=new GSPacketIn((byte[])original.Buffer.Clone(),original.Length);copy.Code=original.Code;copy.ClientID=original.ClientID;copy.Parameter1=original.Parameter1;copy.Parameter2=original.Parameter2;copy.Offset=GSPacketIn.HDR_SIZE;return copy;}
  static void ExitSpa(GamePlayer player,SpaRoom room){if(player.CurrentSpaRoom!=room)return;var exit=new GSPacketIn(169,player.PlayerId);exit.WriteHeader();exit.Offset=GSPacketIn.HDR_SIZE;new Game.Server.Packets.Client.UserExitSpaRoom().HandlePacket(player,exit);}
  static void ExitMarry(GamePlayer player,MarryRoom room){if(player.CurrentMarryRoom!=room)return;var exit=new GSPacketIn(244,player.PlayerId);exit.WriteHeader();exit.Offset=GSPacketIn.HDR_SIZE;new Game.Server.Packets.Client.UserLeaveMarryRoom().HandlePacket(player.Client,exit);}
  public static object InspectScene(GamePlayer player){return new{id=player.PlayerId,active=player.IsActive,room=player.CurrentRoom==null?0:player.CurrentRoom.RoomId,spa=player.CurrentSpaRoom==null?0:player.CurrentSpaRoom.Spa_Room_Info.RoomID,marry=player.CurrentMarryRoom==null?0:player.CurrentMarryRoom.Info.ID,boss=player.IsInWorldBossRoom,idle=IsIdle(player)};}
  public static object LeaveNativeScenes(GamePlayer player){var spa=player.CurrentSpaRoom;var marry=player.CurrentMarryRoom;if(spa!=null)ExitSpa(player,spa);if(marry!=null)ExitMarry(player,marry);var bot=player as BotPlayer;if(bot!=null&&bot.IsInWorldBossRoom)BotWorldBoss36.BeforeOffline(bot);return InspectScene(player);}
  public static bool IsIdle(GamePlayer bot){return bot!=null&&bot.IsActive&&bot.CurrentRoom==null&&bot.CurrentSpaRoom==null&&bot.CurrentMarryRoom==null&&!bot.IsInWorldBossRoom;}
  // Wedding layout is confirmed from the shipped 3.6 ChurchManager/ChurchInviteFrame.
  // Spa layout confirmed by native 191/4 capture: inviter ID/name, room ID/name, password.
  public static bool HandleInvitation(GamePlayer bot,GSPacketIn original,Action<GSPacketIn> receive,bool verifiedSpaHeader=true){
   if(!IsIdle(bot)||receive==null||original==null)return false;
   var packet=CopyPacket(original);
   if(original.Code==249){
    // Captured original 249/4 writer omits VIP fields; the AS3 view in this
    // package declares another layout. Accept only a layout that resolves to
    // the inviter's actual registered native wedding room.
    for(int layout=0;layout<2;layout++)try{
     var body=CopyPacket(original);if(body.ReadByte()!=4)return false;int inviterId=body.ReadInt();body.ReadString();
     if(layout==1){body.ReadBoolean();body.ReadInt();}
     int roomId=body.ReadInt();body.ReadString();string password=body.ReadString();int scene=body.ReadInt();
     var inviter=WorldMgr.GetPlayerById(inviterId);var room=MarryRoomMgr.GetAllMarryRoom().FirstOrDefault(r=>r.Info.ID==roomId);
     if(inviter==null||room==null||inviter.CurrentMarryRoom!=room||(scene!=1&&scene!=2))continue;
     var input=new GSPacketIn(242,bot.PlayerId);input.WriteInt(roomId);input.WriteString(password);input.WriteInt(scene);
     Console.WriteLine("BOT WEDDING INVITE: bot="+bot.PlayerId+" room="+roomId+" layout="+(layout==0?"native":"vip"));
     return QueueJoin(bot,input,receive,()=>bot.CurrentMarryRoom==room,inviter,"婚礼礼堂");
    }catch(System.Exception){/* Try the other known bounded native schema. */}
    return false;
   }
   if(original.Code==191&&verifiedSpaHeader){if(packet.ReadByte()!=4)return false;int inviterId=packet.ReadInt();packet.ReadString();int room=packet.ReadInt();packet.ReadString();string password=packet.ReadString();var inviter=WorldMgr.GetPlayerById(inviterId);if(original.ClientID!=inviterId||inviter==null||inviter.CurrentSpaRoom==null||inviter.CurrentSpaRoom.Spa_Room_Info.RoomID!=room)return false;return QueueSpaEntry(bot,room,password,receive,inviter);}
   return false;
  }
  public static bool QueueSpaEntry(GamePlayer bot,int room,string password,Action<GSPacketIn> receive,GamePlayer inviter=null){if(room<=0)return false;var input=new GSPacketIn(202,bot.PlayerId);input.WriteInt(room);input.WriteString(password??"");return QueueJoin(bot,input,receive,()=>bot.CurrentSpaRoom!=null&&bot.CurrentSpaRoom.Spa_Room_Info.RoomID==room,inviter,"温泉");}
  static bool QueueJoin(GamePlayer bot,GSPacketIn packet,Action<GSPacketIn> receive,Func<bool> entered,GamePlayer inviter,string scene){if(receive==null||!IsIdle(bot)||!Joining.TryAdd(bot.PlayerId,true))return false;ThreadPool.QueueUserWorkItem(delegate{try{Thread.Sleep(50);if(!IsIdle(bot))return;packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);for(int i=0;i<20&&!entered();i++)Thread.Sleep(100);if(entered()&&inviter!=null){var accepted=InvitationAccepted;if(accepted!=null)accepted(bot,inviter,scene);}}catch(Exception e){Console.WriteLine("Scene invitation failed: bot="+bot.PlayerId+" type="+e.GetType().Name);}finally{bool ignored;Joining.TryRemove(bot.PlayerId,out ignored);}});return true;}
  static object PacketEvidence(GSPacketIn packet){var copy=CopyPacket(packet);var bytes=new StringBuilder();while(copy.Offset<copy.Length&&bytes.Length<2048)bytes.Append(copy.ReadByte().ToString("x2"));return new{code=packet.Code,clientId=packet.ClientID,parameter1=packet.Parameter1,parameter2=packet.Parameter2,length=packet.Length,bodyHex=bytes.ToString()};}
  // The gated caller must temporarily mark human as a test human in BotService.
  public static object ProbeSceneChat(GamePlayer human,GamePlayer bot,int channel,string text,Action<GSPacketIn> receiveHuman,Action<Action<GSPacketIn>> humanObserver){
   if(human==null||bot==null||receiveHuman==null||humanObserver==null)throw new ArgumentNullException("Scene chat QA callbacks");
   object response=null;var gate=new object();var arrived=new ManualResetEventSlim(false);
   humanObserver(packet=>{if(packet.Code!=19||packet.ClientID!=bot.PlayerId)return;var p=CopyPacket(packet);int zone=p.ReadInt();int receivedChannel=p.ReadByte();bool team=p.ReadBoolean();string name=p.ReadString(),message=p.ReadString();lock(gate)response=new{zone=zone,channel=receivedChannel,team=team,sender=packet.ClientID,name=name,message=message};arrived.Set();});
   try{var input=new GSPacketIn(19,human.PlayerId);input.WriteByte((byte)channel);input.WriteBoolean(false);input.WriteString(human.PlayerCharacter.NickName);input.WriteString(bot.PlayerCharacter.NickName+": "+(text??"Hello"));input.Offset=GSPacketIn.HDR_SIZE;receiveHuman(input);bool received=arrived.Wait(4000);lock(gate)return new{ok=received,human=human.PlayerId,bot=bot.PlayerId,channel=channel,response=response};}
   finally{humanObserver(null);arrived.Dispose();}
  }
  public static object WithNativeRoom(GamePlayer host,GamePlayer guest,Action<GamePlayer,GamePlayer> whileJoined){
   if(!IsIdle(host)||!IsIdle(guest)||host.PlayerId==guest.PlayerId)throw new InvalidOperationException("Room QA needs two different idle online bots");
   BaseRoom room=null;try{RoomMgr.CreateRoom(host,"本机场景聊天验收","qa-only",eRoomType.Dungeon,20);for(int i=0;i<20&&host.CurrentRoom==null;i++)Thread.Sleep(100);room=host.CurrentRoom;if(room==null)throw new InvalidOperationException("Native room creation pending");RoomMgr.EnterRoom(guest,room.RoomId,room.Password,(int)room.RoomType,true);for(int i=0;i<20&&guest.CurrentRoom!=room;i++)Thread.Sleep(100);if(guest.CurrentRoom==room&&whileJoined!=null)whileJoined(host,guest);return new{ok=guest.CurrentRoom==room,room=room.RoomId,host=host.PlayerId,guest=guest.PlayerId,playing=room.IsPlaying};}
   finally{if(room!=null){if(guest.CurrentRoom==room)RoomMgr.ExitRoom(room,guest);if(host.CurrentRoom==room)RoomMgr.ExitRoom(room,host);}}
  }
  // Gated QA only: caller must provide registered idle bots and the native Receive callbacks.
  // Uses an ephemeral native room factory, never modifies goods or uses DB inserts/updates.
  public static object CaptureNativeSpaInvite(GamePlayer host,GamePlayer guest,Action<GSPacketIn> receiveHost,Action<Action<GSPacketIn>> guestObserver,Action<GamePlayer,GamePlayer> whileJoined=null){
   if(!IsIdle(host)||!IsIdle(guest)||host.PlayerId==guest.PlayerId)throw new InvalidOperationException("Spa QA needs two different idle online bots");
   if(receiveHost==null||guestObserver==null)throw new ArgumentNullException("Spa QA callbacks");SpaRoom room=null;GSPacketIn captured=null;var arrived=new ManualResetEventSlim(false);string stage="select-id";
   try{
    int id=900000000+(Environment.TickCount&0x00ffffff);while(SpaRoomMgr.GetAllSpaRoom().Any(r=>r.Spa_Room_Info.RoomID==id))id++;
    var info=new SpaRoomInfo{RoomID=id,RoomName="本机温泉邀请验收",PlayerID=host.PlayerId,PlayerName=host.PlayerCharacter.NickName,Pwd="qa-only",AvailTime=10,MaxCount=4,MapIndex=1,BeginTime=DateTime.Now,BreakTime=DateTime.Now.AddMinutes(10),RoomIntroduction="临时原生房间，只用于确认邀请包",RoomType=0,ServerID=host.ServerID,RoomNumber=1};
    stage="create-native-room";room=SpaRoomMgr.CreateSpaRoomFromDB(info,600);if(room==null)throw new InvalidOperationException("Native spa fixture creation failed");
    stage="enter-native-host";
    if(host.CurrentSpaRoom!=room&&!room.AddPlayer(host))throw new InvalidOperationException("Native spa fixture host entry refused");
    stage="register-observer";guestObserver(packet=>{if(packet.Code!=191)return;var p=CopyPacket(packet);if(p.ReadByte()!=4)return;captured=CopyPacket(packet);arrived.Set();});
    stage="native-invite-input";
    var invite=new GSPacketIn(191,host.PlayerId);invite.WriteByte(4);invite.WriteInt(guest.PlayerId);invite.Offset=GSPacketIn.HDR_SIZE;receiveHost(invite);
    bool received=arrived.Wait(1000);if(received)for(int i=0;i<20&&guest.CurrentSpaRoom!=room;i++)Thread.Sleep(100);if(guest.CurrentSpaRoom==room&&whileJoined!=null)whileJoined(host,guest);return new{ok=received,room=room.Spa_Room_Info.RoomID,host=host.PlayerId,guest=guest.PlayerId,nativeRegistered=SpaRoomMgr.GetAllSpaRoom().Any(r=>r==room),hostEntered=host.CurrentSpaRoom==room,guestEntered=guest.CurrentSpaRoom==room,invitation=captured==null?null:PacketEvidence(captured),headerMatchesHost=captured!=null&&captured.ClientID==host.PlayerId};
   }catch(Exception e){return new{ok=false,stage=stage,errorType=e.GetType().FullName,error=e.Message,stack=e.StackTrace};}
   finally{guestObserver(null);if(room!=null){ExitSpa(guest,room);ExitSpa(host,room);room.StopTimer();room.StopTimerForGpUpLimit();SpaRoomMgr.RemoveSpaRoom(room);}arrived.Dispose();}
  }
  public static object CaptureNativeMarryInvite(GamePlayer host,GamePlayer guest,Action<GSPacketIn> receiveHost,Action<Action<GSPacketIn>> guestObserver,Action<GamePlayer,GamePlayer> whileJoined=null){
   if(!IsIdle(host)||!IsIdle(guest)||host.PlayerId==guest.PlayerId)throw new InvalidOperationException("Wedding QA needs two different idle online bots");
   if(!host.PlayerCharacter.IsMarried||host.PlayerCharacter.SpouseID<=0)throw new InvalidOperationException("Wedding QA host must already have a real native marriage");
   if(receiveHost==null||guestObserver==null)throw new ArgumentNullException("Wedding QA callbacks");MarryRoom room=null;GSPacketIn captured=null;var arrived=new ManualResetEventSlim(false);string stage="select-id";
   try{
    int id=800000000+(Environment.TickCount&0x00ffffff);while(MarryRoomMgr.GetAllMarryRoom().Any(r=>r.Info.ID==id))id++;
    bool male=host.PlayerCharacter.Sex;var info=new MarryRoomInfo{ID=id,Name="本机礼堂邀请验收",PlayerID=host.PlayerId,PlayerName=host.PlayerCharacter.NickName,GroomID=male?host.PlayerId:host.PlayerCharacter.SpouseID,GroomName=male?host.PlayerCharacter.NickName:host.PlayerCharacter.SpouseName,BrideID=male?host.PlayerCharacter.SpouseID:host.PlayerId,BrideName=male?host.PlayerCharacter.SpouseName:host.PlayerCharacter.NickName,Pwd="qa-only",AvailTime=10,MaxCount=4,GuestInvite=true,MapIndex=1,BeginTime=DateTime.Now,BreakTime=DateTime.Now.AddMinutes(10),RoomIntroduction="临时原生房间，只用于验证场景邀请",ServerID=host.ServerID,IsHymeneal=false,IsGunsaluteUsed=false};
    stage="create-native-room";room=MarryRoomMgr.CreateMarryRoomFromDB(info,600);if(room==null)throw new InvalidOperationException("Native wedding fixture creation failed");
    stage="enter-native-host";if(host.CurrentMarryRoom!=room&&!room.AddPlayer(host))throw new InvalidOperationException("Native wedding fixture host entry refused");
    stage="register-observer";guestObserver(packet=>{if(packet.Code!=249)return;var p=CopyPacket(packet);if(p.ReadByte()!=4)return;captured=CopyPacket(packet);arrived.Set();});
    stage="native-invite-input";var invite=new GSPacketIn(249,host.PlayerId);invite.WriteByte(4);invite.WriteInt(guest.PlayerId);invite.Offset=GSPacketIn.HDR_SIZE;receiveHost(invite);
    bool received=arrived.Wait(1000);if(received)for(int i=0;i<20&&guest.CurrentMarryRoom!=room;i++)Thread.Sleep(100);if(guest.CurrentMarryRoom==room&&whileJoined!=null)whileJoined(host,guest);
    return new{ok=received,room=room.Info.ID,host=host.PlayerId,guest=guest.PlayerId,nativeRegistered=MarryRoomMgr.GetAllMarryRoom().Any(r=>r==room),hostEntered=host.CurrentMarryRoom==room,guestEntered=guest.CurrentMarryRoom==room,hostMap=host.MarryMap,guestMap=guest.MarryMap,invitation=captured==null?null:PacketEvidence(captured)};
   }catch(Exception e){return new{ok=false,stage=stage,errorType=e.GetType().FullName,error=e.Message,stack=e.StackTrace};}
   finally{guestObserver(null);if(room!=null){ExitMarry(guest,room);ExitMarry(host,room);room.StopTimer();room.StopTimerForHymeneal();MarryRoomMgr.RemoveMarryRoom(room);}arrived.Dispose();}
  }
 }
}
