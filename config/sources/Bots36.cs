using System.Reflection;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Net;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web.Script.Serialization;
using Game.Server;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Rooms;
using Game.Base.Packets;
using SqlDataProvider.Data;
using Bussiness;
using Game.Logic;
using ePackageType=Game.Server.Packets.ePackageType;

namespace Local36 {
 public class BotClient:GameClient {
  public BotPlayer Owner;
  public Action<GSPacketIn> PacketObserver;
  public BotClient():base(GameServer.Instance,new byte[8192],new byte[8192]){Out=new AbstractPacketLib(this);var processor=new PacketProcessor(this);foreach(var field in typeof(GameClient).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public))if(field.FieldType==typeof(PacketProcessor))field.SetValue(this,processor);}
  public override void SendTCP(GSPacketIn packet){if(PacketObserver!=null)PacketObserver(packet);if(Owner!=null)BotService.OnPacket(Owner,packet);}
  public void Receive(GSPacketIn packet){packet.WriteHeader();
   // Room commands normally pass through a connected GamePlayer socket queue.
   // Bot clients have no socket; dispatch these two commands to the very same
   // native handlers, including the shared start/cancel lifecycle hooks.
   if(packet.Code==94){int saved=packet.Offset;packet.Offset=GSPacketIn.HDR_SIZE;int command=packet.ReadInt();
    if(command==7){new Game.Server.GameRoom.Handle.GameStart().CommandHandler(Player,packet);return;}
    if(command==11){new Game.Server.GameRoom.Handle.GamePickupCancel().CommandHandler(Player,packet);return;}
    packet.Offset=saved;
   }
   if(packet.Code==68){int saved=packet.Offset;packet.Offset=GSPacketIn.HDR_SIZE;int command=packet.ReadByte();Game.Server.Pet.Handle.IPetCommandHadler handler=null;
    switch(command){case 4:handler=new Game.Server.Pet.Handle.FeedPet();break;case 6:handler=new Game.Server.Pet.Handle.AdoptPet();break;case 17:handler=new Game.Server.Pet.Handle.FightPet();break;}
    if(handler!=null){handler.CommandHandler(Player,packet);return;}packet.Offset=saved;
   }
   foreach(var f in typeof(PacketProcessor).GetFields(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)){
   object value=f.GetValue(f.IsStatic?null:(object)PacketProcessor);object handler=null;var array=value as Array;if(array!=null&&packet.Code>=0&&packet.Code<array.Length)handler=array.GetValue(packet.Code);
   var map=value as IDictionary;if(map!=null){object key=(int)packet.Code;if(f.FieldType.IsGenericType)key=Convert.ChangeType(packet.Code,f.FieldType.GetGenericArguments()[0]);if(map.Contains(key))handler=map[key];}
   if(handler==null)continue;
   var playerMethod=handler.GetType().GetMethod("HandlePacket",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly,null,new[]{typeof(GamePlayer),typeof(GSPacketIn)},null);
   if(playerMethod!=null){playerMethod.Invoke(handler,new object[]{Player,packet});return;}
   var nativeHandler=handler as Game.Server.Packets.Client.IPacketHandler;if(nativeHandler!=null){
    if(packet.Code==59)Console.WriteLine("BOT FORGE DISPATCH: id="+Player.PlayerId+" handler="+handler.GetType().FullName+" cursor="+packet.Offset);
    int status=nativeHandler.HandlePacket(this,packet);
    if(packet.Code==59)Console.WriteLine("BOT FORGE RETURN: id="+Player.PlayerId+" status="+status+" cursor="+packet.Offset);
    return;}
   var method=handler.GetType().GetMethod("HandlePacket",new[]{typeof(GameClient),typeof(GSPacketIn)});if(method==null)continue;method.Invoke(handler,new object[]{this,packet});return;
  }PacketProcessor.HandlePacket(packet);}
 }
 public class BotPlayer:GamePlayer {
  public readonly BotClient BotClient;
  public volatile bool Online=true;
  public BotPlayer(PlayerInfo info,BotClient client):base(info.ID,info.UserName,client,info){BotClient=client;client.Owner=this;client.Player=this;}
  public override bool IsActive {get{return Online;}}
  public override bool IsRobot {get{return false;}}
  public override void Disconnect(){}
  public override void SendTCP(GSPacketIn packet){if(BotClient.PacketObserver!=null)BotClient.PacketObserver(packet);BotService.OnPacket(this,packet);}
 }
 public static partial class BotService {
  static readonly ConcurrentDictionary<int,BotPlayer> Bots=new ConcurrentDictionary<int,BotPlayer>();
  static readonly object BotAdminGate=new object();
  static volatile Dictionary<int,BotProfile> Roster=new Dictionary<int,BotProfile>();
  static PoolScheduler36 pool;static int reload,forceRotation;static volatile int[] desiredOnline=new int[0];
  static readonly Dictionary<int,DateTime> PendingMatch=new Dictionary<int,DateTime>();
  static readonly Dictionary<int,DateTime> MatchPins=new Dictionary<int,DateTime>();
  static readonly ConcurrentDictionary<int,object> MatchStatus=new ConcurrentDictionary<int,object>();

  static DateTime nextRosterPower=DateTime.MinValue;static MatchMember36[] rosterPower=new MatchMember36[0];
  static readonly ConcurrentDictionary<int,DateTime> MatchRequests=new ConcurrentDictionary<int,DateTime>();
  static readonly HashSet<int> ReferenceMatchClones=new HashSet<int>(Enumerable.Range(230,9));
  sealed class MatchAttempt{public BaseRoom Human,Opponent;public DateTime Requested,Created;public string PairPath;}
  static readonly ConcurrentDictionary<int,MatchAttempt> MatchAttempts=new ConcurrentDictionary<int,MatchAttempt>();
  static readonly ConcurrentDictionary<int,bool> MatchedRooms=new ConcurrentDictionary<int,bool>();
  static readonly ConcurrentDictionary<BaseRoom,AbstractGame> WatchedRoomGames=new ConcurrentDictionary<BaseRoom,AbstractGame>();
  static readonly ConcurrentDictionary<int,bool> MatchReservations=new ConcurrentDictionary<int,bool>();
  sealed class Invitation {public GamePlayer Host;public int Room;public DateTime Expires;}
  static readonly ConcurrentDictionary<int,Invitation> Invitations=new ConcurrentDictionary<int,Invitation>();
  static readonly ConcurrentDictionary<int,bool> InviteReservations=new ConcurrentDictionary<int,bool>();
  static readonly ConcurrentDictionary<int,object> InviteGates=new ConcurrentDictionary<int,object>();
  static readonly ConcurrentDictionary<int,object> LastTactics=new ConcurrentDictionary<int,object>();
  static readonly Dictionary<int,DateTime> IdleRooms=new Dictionary<int,DateTime>();
  static readonly HashSet<int> TestHumans=new HashSet<int>();
  static readonly Dictionary<string,DateTime> Replies=new Dictionary<string,DateTime>();
  static readonly string Root=Paths36.Root;
  static readonly string Db=Paths36.Db;
  static Timer timer,combatTimer,eventTimer;static int busy,combatBusy;static volatile bool ShuttingDown;static readonly Dictionary<string,int> CombatTurns=new Dictionary<string,int>();static DateTime nextHall=DateTime.UtcNow.AddSeconds(80);static Random random=new Random();
  sealed class WatchedPve {public PVEGame Game;public int Turn;public string Actor,GameKey;public bool Playing;}
  static readonly Dictionary<int,WatchedPve> WatchedGames=new Dictionary<int,WatchedPve>();
  sealed class PveTurnCursor {public int Turn;public string Actor;}
  static readonly object PveTurnGate=new object();static readonly Dictionary<string,PveTurnCursor> PvePacketTurns=new Dictionary<string,PveTurnCursor>();
  public static void Start(){
   BotCatalog.EnsureSchema();BotLifecycleDriver36.Refresh(true);using(var sql=new SqlConnection(Db)){sql.Open();using(var command=new SqlCommand("UPDATE u SET State=0 FROM Sys_User_Detail u JOIN Local36_Bots b ON b.UserID=u.UserID",sql))command.ExecuteNonQuery();}pool=new PoolScheduler36(Path.Combine(Root,"config","bot-pool-state.json"));ReloadRoster();ApplyPool();Llm36.Start();
   BotRoomInvite36.Install();MatchLifecycle36.Install();HitCredit36.Install();RoadEnergySync36.Install();NativeRewards36.Install(IsBot);NativeRewards36.Completed=CompletedLocalBattle;NativeBotTurn36.IsResidentBot=IsBot;NativeBotTurn36.Prepared=(actor,tactics)=>LastTactics[actor.PlayerDetail.PlayerCharacter.ID]=new{game=actor.Game.Id,turn=actor.Game.TurnIndex,result=tactics};
   SceneInteraction36.InvitationAccepted=(bot,human,scene)=>{var actor=bot as BotPlayer;if(actor!=null)NotifySocial(actor,human,"scene_invitation_accepted","玩家邀请进入"+scene+"，已通过原生入场校验并进入。","我已到"+scene+"了，一起聊聊吧。");};
   MailCare36.MailReceived=(player,sender,title)=>{var bot=player as BotPlayer;if(bot==null)return;var human=WorldMgr.GetPlayerById(sender);if(human!=null&&!IsBot(sender))NotifySocial(bot,human,"gift_received","已通过原生邮件领取玩家赠予的附件，邮件标题："+title+"。道具将按真实使用和穿戴条件处理。","礼物收到了，谢谢你，我会好好使用。");};
   timer=new Timer(Tick,null,2000,2000);combatTimer=new Timer(NativeTurnTick,null,500,250);eventTimer=new Timer(CombatTick,null,1000,1000);StartAdmin();Console.WriteLine("Resident bots initialized: "+Bots.Count);
  }
  static void ReloadRoster(){Roster=BotCatalog.ListProfiles().ToDictionary(p=>p.UserID);pool.UpdateRoster(Roster.Values.Where(p=>p.Enabled).Select(p=>p.UserID));}
  static void AddOnline(int id){try{using(var db=new PlayerBussiness()){
   var info=db.GetUserSingleByUserID(id);if(info==null)throw new Exception("No PlayerInfo");info.IsAutoBot=false;info.State=1;info.IsFirst=4;
   var bot=new BotPlayer(info,new BotClient());if(!bot.LoadFromDatabase())throw new Exception("LoadFromDatabase returned false");bot.PlayerState=Game.Server.Packets.ePlayerState.Online;WeaponIdentity36.Normalize(bot);bot.PlayerCharacter.IsFirst=4;bot.UpdateItemForUser(null);bot.UpdateWeapon(bot.MainBag.GetItemAt(6));bot.UpdateSecondWeapon(bot.MainBag.GetItemAt(15));bot.MainBag.UpdatePlayerProperties();InitializeCombatProfile(bot);BotAppearance36.Refresh(bot,false);bot.UpdateFightPower();CombatPower36.Apply(bot);bot.UpdateProperties();bot.PlayerCharacter.IsAutoBot=false;bot.PlayerCharacter.State=1;
   Presence36.SetLocalZone(bot,GameServer.Instance.Configuration.AreaID,GameServer.Instance.Configuration.ServerID,GameServer.Instance.Configuration.AreaName);
   if(!WorldMgr.AddPlayer(id,bot))throw new Exception("World registration refused");Bots[id]=bot;GameServer.Instance.LoginServer.SendUserOnline(bot.PlayerCharacter);RoomMgr.EnterWaitingRoom(bot);WorldMgr.OnPlayerOnline(id,bot.PlayerCharacter.ConsortiaID);bot.SaveIntoDatabase();Console.WriteLine("POOL ONLINE: "+id+" "+bot.PlayerCharacter.NickName);
  }}catch(Exception e){Console.WriteLine("POOL LOGIN FAILED "+id+": "+e.GetBaseException().Message);}}
  static bool GameFinished(GamePlayer bot){var room=bot.CurrentRoom;if(room==null||!room.IsPlaying)return true;var game=room.Game as BaseGame;return game!=null&&game.GameState==eGameState.ALLSessionStopped;}
  static void ApplyPool(){lock(BotAdminGate){
   if(Interlocked.Exchange(ref forceRotation,0)!=0)pool.State.NextRotationUtc=DateTime.UtcNow.AddSeconds(-1).ToString("o");pool.Advance(DateTime.UtcNow);desiredOnline=pool.State.DesiredIds.ToArray();var desired=new HashSet<int>(desiredOnline);
   foreach(var bot in Bots.Values.Where(b=>!desired.Contains(b.PlayerId))){if(!GameFinished(bot))continue;if(Lifecycle36.ForgeBusy(bot)){lock(bot){var cancelled=Lifecycle36.CancelForge(bot,bot.BotClient.Receive);if(cancelled.acted)bot.SaveIntoDatabase();}continue;}
    if(bot.CurrentRoom!=null){RoomMgr.ExitRoom(bot.CurrentRoom,bot);continue;}
    if(!BotWorldBoss36.BeforeOffline(bot))continue;
    if(!Departure36.Ready(bot,WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot),()=>bot.Online&&!desiredOnline.Contains(bot.PlayerId),(actor,human,text)=>SendPersonal((BotPlayer)actor,human,text)))continue;
    bot.Online=false;bot.PlayerState=Game.Server.Packets.ePlayerState.Offline;bot.PlayerCharacter.State=0;bot.SaveIntoDatabase();WorldMgr.OnPlayerOffline(bot.PlayerId,bot.PlayerCharacter.ConsortiaID);WorldMgr.RemovePlayer(bot.PlayerId);Departure36.Forget(bot.PlayerId);BotPlayer removed;Bots.TryRemove(bot.PlayerId,out removed);Console.WriteLine("POOL OFFLINE: "+bot.PlayerId);
   }
   foreach(int id in desired){if(Bots.Count>=50)break;if(!Bots.ContainsKey(id))AddOnline(id);}
  }}
  static void CombatTick(object state){if(ShuttingDown||Interlocked.Exchange(ref combatBusy,1)!=0)return;try{
   var activeGames=Bots.Values.Select(b=>b.CurrentRoom==null?null:b.CurrentRoom.Game as PVEGame).Where(g=>g!=null).Distinct().ToArray();
   foreach(var game in activeGames)BotWorldBoss36.Observe(game);
   foreach(var game in activeGames)if(!WatchedGames.ContainsKey(game.Id))WatchedGames[game.Id]=new WatchedPve{Game=game,Turn=game.TurnIndex,Actor=CurrentActorName(game),GameKey=PveGameKey(game),Playing=game.GameState==eGameState.Playing};
   foreach(var old in WatchedGames.Values.ToArray()){
    bool playing=old.Game.GameState==eGameState.Playing;
    if(old.Playing&&(old.Turn!=old.Game.TurnIndex||!playing)){
     foreach(var actor in old.Game.GetAllLivingPlayers()){
      int id=actor.PlayerDetail.PlayerCharacter.ID;if(!IsBot(id))continue;
      BattleReply(id,old.GameKey,old.Turn,"PVE回合结束，行动者："+old.Actor+"。"+BattleSnapshot(old.Game,id));
     }
    }
    old.Turn=old.Game.TurnIndex;old.Actor=CurrentActorName(old.Game);old.Playing=playing;old.GameKey=PveGameKey(old.Game);
    if(!activeGames.Contains(old.Game))WatchedGames.Remove(old.Game.Id);
   }

  }catch(Exception e){Console.WriteLine("BOT PVE AI: "+e.GetBaseException().Message);}finally{Interlocked.Exchange(ref combatBusy,0);}}
  static int turnBusy;
  static void NativeTurnTick(object unused){if(ShuttingDown||Interlocked.Exchange(ref turnBusy,1)!=0)return;try{
   foreach(var bot in Bots.Values){try{var room=bot.CurrentRoom;if(room==null)continue;var game=room.Game as BaseGame;if(game==null||game.GameState!=eGameState.Playing)continue;
   var actor=game.GetAllLivingPlayers().FirstOrDefault(p=>p.PlayerDetail.PlayerCharacter.ID==bot.PlayerId);if(actor==null||!actor.IsAttacking||!actor.IsLiving)continue;
   var pve=game as PVEGame;
   Game.Logic.Phy.Object.Living target;
   if(pve!=null)target=BotWorldBoss36.Targets(pve,actor).Concat(EnemyLivings(game)).Where(n=>n.IsLiving&&n.Team!=actor.Team).OrderByDescending(n=>room.RoomType==eRoomType.WordBossFight&&n.Config!=null&&n.Config.IsWorldBoss).ThenBy(n=>Math.Abs(n.X-actor.X)).FirstOrDefault();
   else target=game.GetAllLivingPlayers().Where(n=>n.IsLiving&&n.Team!=actor.Team).OrderBy(n=>Math.Abs(n.X-actor.X)).FirstOrDefault();
   NativeBotTurn36.Queue(game,actor,target);
   }catch(Exception e){Console.WriteLine("BOT NATIVE TURN "+bot.PlayerId+": "+e.GetType().Name);}}
  }finally{Interlocked.Exchange(ref turnBusy,0);}}
  internal static bool HasPendingInvitation(int id){return Invitations.ContainsKey(id)||InviteReservations.ContainsKey(id)||MatchReservations.ContainsKey(id);}
  static IEnumerable<Game.Logic.Phy.Object.Living> EnemyLivings(BaseGame game){var npcs=game.FindAllNpcLiving().Cast<Game.Logic.Phy.Object.Living>();var pve=game as PVEGame;return pve==null?npcs:npcs.Concat(pve.FindAllBoss());}
  static string LivingName(Game.Logic.Phy.Object.Living actor){var player=actor as Game.Logic.Phy.Object.Player;if(player!=null)return player.PlayerDetail.PlayerCharacter.NickName;return actor==null?"":actor.NpcInfo!=null&&!String.IsNullOrEmpty(actor.NpcInfo.Name)?actor.NpcInfo.Name:String.IsNullOrEmpty(actor.Name)?actor.GetType().Name:actor.Name;}
  static string PveGameKey(PVEGame game){return "road/"+game.Id+"/"+game.SessionId;}
  static void NativePveNextTurn(BotPlayer bot,int livingId){var room=bot.CurrentRoom;var game=room==null?null:room.Game as PVEGame;if(game==null)return;
   var actor=game.GetAllLivingPlayers().Cast<Game.Logic.Phy.Object.Living>().Concat(EnemyLivings(game)).FirstOrDefault(p=>p.Id==livingId);
   var next=new PveTurnCursor{Turn=game.TurnIndex,Actor=actor==null?CurrentActorName(game):LivingName(actor)};PveTurnCursor old=null;string key=PveGameKey(game)+"/"+bot.PlayerId;
   lock(PveTurnGate){if(PvePacketTurns.TryGetValue(key,out old)&&old.Turn==next.Turn)return;if(PvePacketTurns.Count>10000)PvePacketTurns.Clear();PvePacketTurns[key]=next;}
   if(old!=null)BattleReply(bot.PlayerId,PveGameKey(game),old.Turn,"PVE回合结束，原生回合："+old.Turn+"，行动者："+old.Actor+"。"+BattleSnapshot(game,bot.PlayerId));
  }
  static void NativePveFinish(BotPlayer bot){var room=bot.CurrentRoom;var game=room==null?null:room.Game as PVEGame;if(game==null)return;string key=PveGameKey(game)+"/"+bot.PlayerId;PveTurnCursor last;lock(PveTurnGate){if(!PvePacketTurns.TryGetValue(key,out last))return;PvePacketTurns.Remove(key);}BattleReply(bot.PlayerId,PveGameKey(game),last.Turn,"PVE回合结束，原生结束包，回合："+last.Turn+"，行动者："+last.Actor+"。"+BattleSnapshot(game,bot.PlayerId));}
  static string CurrentActorName(BaseGame game){var pve=game as PVEGame;Game.Logic.Phy.Object.Living actor=pve==null?game.CurrentLiving:pve.CurrentTurnLiving;if(actor==null){for(var type=game.GetType();type!=null;type=type.BaseType){var field=type.GetField("m_currentLiving",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.DeclaredOnly);if(field!=null){actor=field.GetValue(game) as Game.Logic.Phy.Object.Living;if(actor!=null)break;}}}var player=actor as Game.Logic.Phy.Object.Player;return player!=null?player.PlayerDetail.PlayerCharacter.NickName:actor==null?"":String.IsNullOrEmpty(actor.Name)?actor.GetType().Name:actor.Name;}
  static string BattleSnapshot(BaseGame game,int self){return new JavaScriptSerializer().Serialize(new{self,game=game.Id,turn=game.TurnIndex,state=game.GameState.ToString(),actor=CurrentActorName(game),players=game.GetAllLivingPlayers().Select(p=>new{name=p.PlayerDetail.PlayerCharacter.NickName,level=p.PlayerDetail.PlayerCharacter.Grade,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team}).ToArray(),monsters=EnemyLivings(game).Where(p=>p.IsLiving).Take(8).Select(p=>new{name=p.Name,hp=p.Blood,maxHp=p.MaxBlood}).ToArray()});}
  static readonly ConcurrentDictionary<string,bool> BattleEvents=new ConcurrentDictionary<string,bool>();
  static void BattleReply(int id,string game,int turn,string summary){string key=game+"/"+id+"/"+turn;if(!BattleEvents.TryAdd(key,true))return;BotPlayer bot;if(!Bots.TryGetValue(id,out bot)||!bot.Online)return;
   var room=bot.CurrentRoom;var battle=room==null?null:room.Game;Action<string> reply=answer=>{if(bot.Online&&room!=null&&Object.ReferenceEquals(bot.CurrentRoom,room)&&Object.ReferenceEquals(room.Game,battle))Speak(bot,answer,null,bot.IsInWorldBossRoom?20:5);};
   string fallback=new[]{"这一回合结束了，下一轮一起注意风向。","下一回合我会继续配合，留意对面的站位。","这一轮结束，调整好角度再继续。"}[Math.Abs(turn%3)];
   if(!Llm36.Submit(id,"turn_end",summary,"battle/"+game,reply,()=>reply(fallback)))reply(fallback);
  }
  internal static void NotifySocial(BotPlayer bot,GamePlayer human,string eventName,string detail,string fallback){
   if(human==null||IsBot(human.PlayerId))return;Action<string> reply=text=>{if(bot.Online&&WorldMgr.GetPlayerById(human.PlayerId)==human)SendPersonal(bot,human,text);};
   string prompt="游戏互动事件："+eventName+"。玩家"+human.PlayerCharacter.NickName+"与角色"+bot.PlayerCharacter.NickName+"发生了已由服务端确认的互动："+detail+"。请结合角色性格和已有互动记忆，主动对该玩家发一句自然回应；不要虚构尚未完成的操作。";
   if(!Llm36.Submit(bot.PlayerId,"private_"+eventName,prompt,"player/"+human.PlayerId,reply,()=>reply(fallback)))reply(fallback);
  }
  public static bool IsBot(int id){lock(TestHumans)return Roster.ContainsKey(id)&&!TestHumans.Contains(id);}
  static void InitializeCombatProfile(BotPlayer bot){
   foreach(var equipped in bot.MainBag.GetItems().Where(i=>i!=null&&i.Place>=0&&i.Place<31&&i.Template!=null&&new[]{1,5,7,13}.Contains(i.Template.CategoryID)).ToArray()){var item=equipped;ItemInfo.OpenHole(ref item);bot.MainBag.UpdateItem(item);}
   WeaponIdentity36.Normalize(bot);BotAppearance36.Refresh(bot,false);NativeStats36.Recalculate(bot);
  }
  static void Tick(object state){if(ShuttingDown||Interlocked.Exchange(ref busy,1)!=0)return;try{
   if(Interlocked.Exchange(ref reload,0)!=0){ReloadRoster();nextRosterPower=DateTime.MinValue;BotLifecycleDriver36.Refresh(true);}else BotLifecycleDriver36.Refresh(false);ApplyPool();RetryInvitations();AutoMatch();CombatPower36.Observe(WorldMgr.GetAllPlayers());
   foreach(var bot in Bots.Values.ToArray()){try{if(NativePetAdopt36.IsBusy(bot.PlayerId))continue;
    if(WorldMgr.GetPlayerById(bot.PlayerId)==null){WorldMgr.AddPlayer(bot.PlayerId,bot);GameServer.Instance.LoginServer.SendUserOnline(bot.PlayerCharacter);RoomMgr.EnterWaitingRoom(bot);}
    if(bot.PlayerState==Game.Server.Packets.ePlayerState.Offline)bot.PlayerState=Game.Server.Packets.ePlayerState.Online;bot.PlayerCharacter.State=1;BotLifecycleDriver36.Maintenance(bot);
    if(!Lifecycle36.ForgeBusy(bot))lock(bot){string mailAction=MailCare36.NativeTick(bot,bot.BotClient.Receive,BotLifecycleDriver36.CanUseGrowthItems(bot));if(mailAction!=null){WeaponIdentity36.Normalize(bot);InitializeCombatProfile(bot);bot.SaveIntoDatabase();RankSync36.MarkDirty();Console.WriteLine("BOT MAIL CARE: "+bot.PlayerId+" "+mailAction);}}
    RankSync36.Observe(bot);var room=bot.CurrentRoom;WatchRoomBattle(room);MissionReady36.Tick(bot);if(room!=null&&!room.IsPlaying&&room.Host!=bot)RoomMgr.UpdatePlayerState(bot,1);
    if(room!=null&&!room.GetPlayers().Any(p=>!IsBot(p.PlayerId)&&!p.IsRobot)){
     var game=room.Game as BaseGame;bool ended=game!=null&&game.GameState==eGameState.ALLSessionStopped;
     if(ended||(!room.IsPlaying&&!BotLifecycleDriver36.OwnsRoom(bot))){DateTime since;if(!IdleRooms.TryGetValue(room.RoomId,out since))IdleRooms[room.RoomId]=DateTime.UtcNow;else if((DateTime.UtcNow-since).TotalSeconds>30)RoomMgr.ExitRoom(room,bot);}
    }else if(room!=null)IdleRooms.Remove(room.RoomId);
   }catch(Exception e){Console.WriteLine("BOT MAINTENANCE "+bot.PlayerId+": "+e.GetType().Name);}}
   RankSync36.Tick();Presence36.RemoveLegacyPlaceholders();Presence36.Synchronize(Bots.Values,WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot));AutoFriends();AutoMarriage();AutoAcademy();BotConsortia36.Tick(Bots.Values);BotConversation36.Tick(Bots.Values,WorldMgr.GetAllPlayers());BotLifecycleDriver36.Tick(Bots.Values,id=>!ShuttingDown&&desiredOnline.Contains(id)&&Bots.ContainsKey(id)&&!HasPendingInvitation(id));BotWorldBoss36.Tick(Bots.Values.Where(p=>BotLifecycleDriver36.AllowsWorldBoss(p.PlayerId)&&!Lifecycle36.ForgeBusy(p)&&(p.CurrentRoom!=null||(!HasPendingInvitation(p.PlayerId)&&!MatchReservations.ContainsKey(p.PlayerId)&&(!MatchPins.ContainsKey(p.PlayerId)||MatchPins[p.PlayerId]<DateTime.UtcNow)))));

  }catch(Exception e){Console.WriteLine("BOT TICK: "+e.Message);}finally{Interlocked.Exchange(ref busy,0);}}
  static void AutoFriends(){using(var sql=new SqlConnection(Db)){sql.Open();var pairs=new List<int[]>();using(var c=new SqlCommand("SELECT f.UserID,f.FriendID FROM Sys_User_Friends f JOIN Local36_Bots b ON b.UserID=f.FriendID WHERE f.IsExist=1 AND f.Relation=0 AND NOT EXISTS(SELECT 1 FROM Sys_User_Friends r WHERE r.UserID=f.FriendID AND r.FriendID=f.UserID AND r.IsExist=1 AND r.Relation=0)",sql))using(var r=c.ExecuteReader())while(r.Read())pairs.Add(new[]{r.GetInt32(0),r.GetInt32(1)});
   foreach(var pair in pairs){BotPlayer bot;if(!Bots.TryGetValue(pair[1],out bot))continue;using(var c=new SqlCommand("DECLARE @id int; EXEC SP_Users_Friends_Add @ID=@id OUTPUT,@UserID=@bot,@FriendID=@human,@AddDate=@date,@Remark=N'',@IsExist=1,@Relation=0",sql)){c.Parameters.AddWithValue("@bot",pair[1]);c.Parameters.AddWithValue("@human",pair[0]);c.Parameters.AddWithValue("@date",DateTime.Now);c.ExecuteNonQuery();}bot.FriendsAdd(pair[0],0);var human=WorldMgr.GetPlayerById(pair[0]);if(human!=null){human.Out.SendFriendState(bot.PlayerId,1,0,0);NotifySocial(bot,human,"friend_added","玩家添加好友，角色已经自动回加好友。","好友已添加，有副本可以邀请我。");}Console.WriteLine("Bot accepted friend: "+pair[1]+" <- "+pair[0]);}
  }}
  static void AutoAcademy(){foreach(var human in WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot))AcceptAcademy(human);}
  static void AcceptAcademy(GamePlayer human){foreach(var bot in Bots.Values){var req=AcademyMgr.GetRequest(human.PlayerId,bot.PlayerId);if(req==null)continue;
   GamePlayer master=req.Type==(int)Game.Server.Packets.eAcademyType.ACADEMY_FOR_MASTER?(GamePlayer)bot:human;GamePlayer student=master==bot?human:(GamePlayer)bot;
   if(AcademyMgr.CheckCanMaster(master.PlayerCharacter.Grade)&&AcademyMgr.CheckCanApp(student.PlayerCharacter.Grade)&&AcademyMgr.AddApprentice(master,student)){AcademyMgr.RemoveRequest(req);human.Out.SendAcademyAppState(human.PlayerCharacter,(int)Game.Server.Packets.eAcademyType.UPDATE_APP_STATES);NotifySocial(bot,human,"academy_accepted","玩家发起师徒申请，符合条件并已建立师徒关系。","我同意师徒申请，一起完成师徒任务吧。");Console.WriteLine("Bot accepted academy: master="+master.PlayerId+" student="+student.PlayerId);}
  }}
  static void AutoMarriage(){using(var sql=new SqlConnection(Db)){sql.Open();var requests=new List<int[]>();using(var c=new SqlCommand("SELECT a.ID,a.UserID,a.ApplyUserID FROM Sys_User_Marry_Apply a JOIN Local36_Bots b ON b.UserID=a.UserID JOIN Sys_User_Detail bot ON bot.UserID=a.UserID JOIN Sys_User_Detail human ON human.UserID=a.ApplyUserID WHERE a.IsExist=1 AND a.IsDeal=0 AND a.ApplyType=1 AND a.ApplyResult=0 AND bot.IsMarried=0 AND human.IsMarried=0 AND bot.Sex<>human.Sex",sql))using(var r=c.ExecuteReader())while(r.Read())requests.Add(new[]{r.GetInt32(0),r.GetInt32(1),r.GetInt32(2)});
   foreach(var req in requests){BotPlayer bot;if(!Bots.TryGetValue(req[1],out bot))continue;var info=new MarryApplyInfo{UserID=req[2],ApplyUserID=bot.PlayerId,ApplyUserName=bot.PlayerCharacter.NickName,ApplyType=2,ApplyResult=true,LoveProclamation=""};int id=0;using(var db=new PlayerBussiness())if(db.SavePlayerMarryNotice(info,req[0],ref id)){bot.LoadMarryProp();GameServer.Instance.LoginServer.SendUpdatePlayerMarriedStates(req[2]);var human=WorldMgr.GetPlayerById(req[2]);if(human!=null)NotifySocial(bot,human,"marriage_accepted","玩家发起求婚，符合原生条件并已同意，结婚关系已更新。","我同意啦，祝我们游戏愉快。");Console.WriteLine("Bot accepted marriage: "+bot.PlayerId+" / "+req[2]);}}
  }}
  public static void MatchRequested(GamePlayer player){var room=player.CurrentRoom;if(room==null||room.RoomType!=eRoomType.Match||room.Host!=player||room.Game!=null)return;MatchRequests[room.RoomId]=DateTime.UtcNow;Console.WriteLine("MATCH USER REQUEST: user="+player.PlayerId+" room="+room.RoomId);}
  public static void ProxyStopped(BaseRoom room,AbstractGame stopped){
   if(room==null||room.RoomType!=eRoomType.Match||stopped==null)return;DateTime before;MatchRequests.TryGetValue(room.RoomId,out before);
   ThreadPool.QueueUserWorkItem(_=>{try{for(int i=0;i<20&&room.Game==stopped&&room.Game!=null;i++)Thread.Sleep(100);
    lock(room){DateTime current;if(room.Game!=null||MatchRequests.TryGetValue(room.RoomId,out current)&&current>before)return;
     if(room.IsPlaying){room.IsPlaying=false;room.ResetPlayerState();room.SendCancelPickUp();RoomMgr.WaitingRoom.SendUpdateRoom(RoomMgr.WaitingRoom.GetPlayersSafe().ToList(),new List<BaseRoom>{room});Console.WriteLine("MATCH NATIVE STOP RELEASED: room="+room.RoomId);}
     ExitFinishedBots(room);
    }
   }catch(Exception e){Console.WriteLine("MATCH NATIVE STOP: "+e.GetType().Name);}});
  }
  static readonly ConcurrentDictionary<int,BaseRoom> PendingBattleExits=new ConcurrentDictionary<int,BaseRoom>();
  static void ExitFinishedBots(BaseRoom room){foreach(var bot in room.GetPlayers().Where(p=>Roster.ContainsKey(p.PlayerId)).ToArray()){
   if(!PendingBattleExits.TryAdd(bot.PlayerId,room))continue;
   RoomMgr.ExitRoom(room,bot);Console.WriteLine("BOT BATTLE EXIT: bot="+bot.PlayerId+" room="+room.RoomId);
   ThreadPool.QueueUserWorkItem(_=>{try{for(int i=0;i<50&&Object.ReferenceEquals(bot.CurrentRoom,room);i++)Thread.Sleep(100);}finally{BaseRoom old;PendingBattleExits.TryRemove(bot.PlayerId,out old);}});
  }}
  static void WatchRoomBattle(BaseRoom room){if(room==null||room.Game==null)return;var game=room.Game;AbstractGame previous;if(WatchedRoomGames.TryGetValue(room,out previous)&&Object.ReferenceEquals(previous,game))return;WatchedRoomGames[room]=game;
   game.GameStopped+=stopped=>{ThreadPool.QueueUserWorkItem(_=>{try{for(int i=0;i<20&&Object.ReferenceEquals(room.Game,game);i++)Thread.Sleep(100);lock(room){if(room.Game!=null&&!Object.ReferenceEquals(room.Game,game))return;room.IsPlaying=false;ExitFinishedBots(room);}}catch(Exception e){Console.WriteLine("BOT NATIVE FINISH: "+e.GetType().Name);}finally{AbstractGame old;WatchedRoomGames.TryRemove(room,out old);}});};
  }
  static void CompletedLocalBattle(BaseGame game){foreach(var bot in Bots.Values.ToArray()){var room=bot.CurrentRoom;if(room!=null&&Object.ReferenceEquals(room.Game,game))ExitFinishedBots(room);}}
  public static void CancelMatchingRoom(BaseRoom room,string reason){
   MatchAttempt attempt;lock(room){DateTime unusedTime;MatchRequests.TryRemove(room.RoomId,out unusedTime);bool unused;MatchedRooms.TryRemove(room.RoomId,out unused);MatchAttempts.TryRemove(room.RoomId,out attempt);if(attempt!=null&&attempt.PairPath!=null&&File.Exists(attempt.PairPath))File.Delete(attempt.PairPath);}
   CancelNativeQueue(room);if(attempt!=null&&attempt.Opponent!=null&&attempt.Opponent.Game==null){CancelNativeQueue(attempt.Opponent);foreach(var p in attempt.Opponent.GetPlayers().Where(p=>IsBot(p.PlayerId)).ToArray())RoomMgr.ExitRoom(attempt.Opponent,p);}
   MatchStatus[room.RoomId]=new{state=reason};Console.WriteLine("MATCH CANCELLED: room="+room.RoomId+" reason="+reason);
  }
  static void CancelNativeQueue(BaseRoom room){lock(room){if(room.Game!=null)return;var server=room.BattleServer;if(server!=null){server.RemoveRoom(room);RoomMgr.CancelPickup(server,room);}room.IsPlaying=false;room.SendCancelPickUp();RoomMgr.WaitingRoom.SendUpdateRoom(RoomMgr.WaitingRoom.GetPlayersSafe().ToList(),new List<BaseRoom>{room});}}
  static void AutoMatch(){
   var active=new HashSet<int>();
   foreach(var entry in MatchAttempts.ToArray()){var attempt=entry.Value;if(attempt.Human.Game!=null){DateTime old;MatchRequests.TryRemove(entry.Key,out old);MatchAttempt gone;MatchAttempts.TryRemove(entry.Key,out gone);continue;}if(!attempt.Human.IsPlaying||(DateTime.UtcNow-attempt.Created).TotalSeconds>20)CancelMatchingRoom(attempt.Human,"opponent_start_timeout_returned_to_room");}
   foreach(var human in WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot&&p.CurrentRoom!=null).ToArray()){
    var room=human.CurrentRoom;if(room==null||room.RoomType!=eRoomType.Match||room.Host!=human||!room.IsPlaying||room.Game!=null)continue;
    DateTime requested;if(!MatchRequests.TryGetValue(room.RoomId,out requested))continue;
    active.Add(room.RoomId);if((DateTime.UtcNow-requested).TotalSeconds>=55){CancelMatchingRoom(room,"match_timeout_returned_to_room");continue;}if(MatchedRooms.ContainsKey(room.RoomId))continue;
    DateTime start;if(!PendingMatch.TryGetValue(room.RoomId,out start)||start<requested){PendingMatch[room.RoomId]=requested;start=requested;}
    if((DateTime.UtcNow-start).TotalSeconds<18){MatchStatus[room.RoomId]=new{state="waiting_for_real_players",seconds=(int)(DateTime.UtcNow-start).TotalSeconds};continue;}
    var team=room.GetPlayers().Select(MatchMember).ToArray();if(team.Length<1||team.Length>4)continue;
    if(HasRealOpponent(room,team)){MatchStatus[room.RoomId]=new{state="real_player_opponent_has_priority",team=team};continue;}
    var choices=MatchPolicy36.SelectExisting(team,Bots.Values.Where(p=>MatchIdle(p)&&desiredOnline.Contains(p.PlayerId)&&!ReferenceMatchClones.Contains(p.PlayerId)).Select(MatchMember));
    if(choices.Length!=team.Length){MatchStatus[room.RoomId]=new{state="waiting_for_available_existing_lobby_bots",count=team.Length};continue;}var opponents=choices.Select(p=>Bots[p.Id]).ToArray();
    foreach(var choice in choices)MatchPins[choice.Id]=DateTime.UtcNow.AddMinutes(10);
    MatchStatus[room.RoomId]=new{state="creating_opponent_party",team=team,opponents=choices};
    var acquired=new List<int>();bool reserved=true;foreach(var opponent in opponents)if(MatchReservations.TryAdd(opponent.PlayerId,true))acquired.Add(opponent.PlayerId);else{reserved=false;break;}
    if(!reserved){foreach(int id in acquired){bool unused;MatchReservations.TryRemove(id,out unused);}continue;}
    PendingMatch.Remove(room.RoomId);MatchedRooms[room.RoomId]=true;
    ThreadPool.QueueUserWorkItem(_=>{bool started=false;BaseRoom created=null;try{
     DateTime latest;if(!MatchRequests.TryGetValue(room.RoomId,out latest)||latest!=requested||!room.IsPlaying||room.Game!=null||room.GetPlayers().Count!=team.Length||opponents.Any(p=>p.CurrentRoom!=null)||HasRealOpponent(room,team))return;
     foreach(var opponent in opponents)if(opponent.IsInWorldBossRoom&&!BotWorldBoss36.BeforeOffline(opponent))return;
     var host=opponents[0];RoomMgr.CreateRoom(host,"竞技练习", "",eRoomType.Match,room.TimeMode);
     for(int attempt=0;attempt<20&&host.CurrentRoom==null;attempt++)Thread.Sleep(150);
     var target=host.CurrentRoom;created=target;if(target==null)throw new Exception("Opponent room creation timed out");target.GameType=room.GameType;
     OpenBotSeats(host,team.Length);
     foreach(var opponent in opponents.Skip(1))RoomMgr.EnterRoom(opponent,target.RoomId,target.Password,(int)eRoomType.Match,true);
     for(int attempt=0;attempt<20&&target.GetPlayers().Count!=team.Length;attempt++)Thread.Sleep(150);
     if(target.GetPlayers().Count!=team.Length)throw new Exception("Existing opponent party size changed while matching");
     foreach(var opponent in opponents)RoomMgr.UpdatePlayerState(opponent,1);RoomMgr.StartGame(target);
     for(int attempt=0;attempt<25&&!target.IsPlaying;attempt++)Thread.Sleep(100);
     DateTime currentRequest;if(!target.IsPlaying||!MatchRequests.TryGetValue(room.RoomId,out currentRequest)||currentRequest!=requested)throw new Exception("Native opponent queue refused or user cancelled");
     lock(room){if(!MatchRequests.TryGetValue(room.RoomId,out currentRequest)||currentRequest!=requested)throw new Exception("Player cancelled before pairing");var attempt=new MatchAttempt{Human=room,Opponent=target,Requested=requested,Created=DateTime.UtcNow};MatchAttempts[room.RoomId]=attempt;if(room.Game==null&&target.Game==null){string directory=Path.Combine(Root,"config","match-pairs");Directory.CreateDirectory(directory);attempt.PairPath=Path.Combine(directory,Guid.NewGuid().ToString("N")+".json");string temp=attempt.PairPath+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new{id=Path.GetFileNameWithoutExtension(attempt.PairPath),players=team.Select(m=>m.Id).ToArray(),bots=opponents.Select(p=>p.PlayerId).ToArray(),expiresUtc=DateTime.UtcNow.AddSeconds(25)}));File.Move(temp,attempt.PairPath);}started=true;}
     Console.WriteLine("BOT MATCH EQUAL TEAMS: "+room.RoomId+" vs "+target.RoomId+" count="+team.Length+" opponents="+String.Join(",",opponents.Select(p=>p.PlayerId)));
    }catch(Exception e){Console.WriteLine("BOT MATCH: "+e.Message);}finally{foreach(var opponent in opponents){bool unused;MatchReservations.TryRemove(opponent.PlayerId,out unused);}if(!started){if(created!=null)foreach(var opponent in opponents.Where(p=>p.CurrentRoom==created))RoomMgr.ExitRoom(created,opponent);bool unused;MatchedRooms.TryRemove(room.RoomId,out unused);}}});
   }
   foreach(int id in PendingMatch.Keys.Where(id=>!active.Contains(id)).ToArray())PendingMatch.Remove(id);
   foreach(int id in MatchedRooms.Keys.Where(id=>!active.Contains(id)).ToArray()){bool unused;MatchedRooms.TryRemove(id,out unused);}
   foreach(int id in MatchStatus.Keys.Where(id=>!active.Contains(id)).ToArray()){object unused;MatchStatus.TryRemove(id,out unused);}
  }
  static bool HasRealOpponent(BaseRoom room,MatchMember36[] team){
   return WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot&&p.CurrentRoom!=null).Select(p=>p.CurrentRoom).Distinct().Any(other=>other!=room&&other.RoomType==eRoomType.Match&&other.GameType==room.GameType&&other.IsPlaying&&other.Game==null&&MatchPolicy36.TeamsCompatible(team,other.GetPlayers().Select(MatchMember)));
  }
  public static void RequestRoomInvitation(int botId,GamePlayer host){
   if(host==null||host.CurrentRoom==null||!IsBot(botId))return;var room=host.CurrentRoom;
   Invitations[botId]=new Invitation{Host=host,Room=room.RoomId,Expires=DateTime.UtcNow.AddMinutes(3)};
   Console.WriteLine("BOT INVITE REQUEST: host="+host.PlayerId+" bot="+botId+" room="+room.RoomId+" members="+room.GetPlayers().Count+" capacity="+room.PlacesCount);
   BotPlayer bot;if(Bots.TryGetValue(botId,out bot)){AcceptInvite(bot,host,room.RoomId);return;}
   lock(BotAdminGate){if(pool.Promote(new[]{botId},Bots.Values.Where(MatchIdle).Select(p=>p.PlayerId)))desiredOnline=pool.State.DesiredIds.ToArray();}
  }
  static bool MatchIdle(BotPlayer p){return !NativePetAdopt36.IsBusy(p.PlayerId)&&p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&p.Online&&!MatchReservations.ContainsKey(p.PlayerId)&&!InviteReservations.ContainsKey(p.PlayerId)&&!Invitations.ContainsKey(p.PlayerId)&&!Lifecycle36.ForgeBusy(p);}
  static MatchMember36 MatchMember(GamePlayer player){return new MatchMember36{Id=player.PlayerId,Level=player.PlayerCharacter.Grade,Power=player.PlayerCharacter.FightPower};}
  internal static void OpenBotSeats(GamePlayer host,int count){if(host==null||host.CurrentRoom==null||host.CurrentRoom.Host!=host||count<1||count>4)throw new InvalidOperationException("Native room host and party size required");var room=host.CurrentRoom;var states=(int[])typeof(BaseRoom).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(int[])).GetValue(room);foreach(int seat in Enumerable.Range(1,3).Where(i=>states[i]==0).Take(Math.Max(0,count-room.PlacesCount)))RoomMgr.UpdateRoomPos(room,seat,false,-1,-100);for(int i=0;i<30&&room.PlacesCount<count;i++)Thread.Sleep(100);if(room.PlacesCount<count)throw new InvalidOperationException("Native open seats failed: capacity="+room.PlacesCount+" requested="+count);}
  static void RetryInvitations(){foreach(var entry in Invitations){var request=entry.Value;BotPlayer bot;
   if(DateTime.UtcNow>request.Expires||request.Host.CurrentRoom==null||request.Host.CurrentRoom.RoomId!=request.Room){Invitation old;Invitations.TryRemove(entry.Key,out old);continue;}
   if(!Bots.TryGetValue(entry.Key,out bot)||!bot.Online)continue;
   if(bot.CurrentRoom!=null&&bot.CurrentRoom.RoomId==request.Room&&bot.CurrentRoom.GetPlayers().Contains(bot)){Invitation old;Invitations.TryRemove(entry.Key,out old);continue;}
   AcceptInvite(bot,request.Host,request.Room);
  }}
  static void AcceptInvite(BotPlayer bot,GamePlayer host,int roomId){
   var room=host.CurrentRoom;if(room==null||room.RoomId!=roomId||!bot.Online||MatchReservations.ContainsKey(bot.PlayerId))return;
   if(!InviteReservations.TryAdd(bot.PlayerId,true))return;
   ThreadPool.QueueUserWorkItem(_=>{try{lock(InviteGates.GetOrAdd(roomId,id=>new object()))lock(bot){
    if(host.CurrentRoom!=room||!bot.Online)return;
    if(bot.CurrentRoom==room&&room.GetPlayers().Contains(bot)){RoomMgr.UpdatePlayerState(bot,1);MissionReady36.Tick(bot);return;}
    if(bot.CurrentRoom!=null&&!bot.CurrentRoom.GetPlayers().Contains(bot)&&bot.CurrentRoom.Game==null){Console.WriteLine("BOT INVITE ORPHAN ROOM: bot="+bot.PlayerId+" old="+bot.CurrentRoom.RoomId);bot.CurrentRoom=null;RoomMgr.EnterWaitingRoom(bot);}
    if(bot.CurrentRoom!=null){var old=bot.CurrentRoom;var previous=old.Game as BaseGame;bool finished=previous!=null&&(previous.GameState==eGameState.GameOver||previous.GameState==eGameState.ALLSessionStopped);if((old.IsPlaying&&!finished)||old.GetPlayers().Any(p=>!IsBot(p.PlayerId)))return;RoomMgr.ExitRoom(old,bot);for(int i=0;i<20&&bot.CurrentRoom==old;i++)Thread.Sleep(100);if(bot.CurrentRoom!=null)return;}
    if(bot.CurrentSpaRoom!=null||bot.CurrentMarryRoom!=null)return;
    if(Lifecycle36.ForgeBusy(bot)){var cancel=Lifecycle36.CancelForge(bot,bot.BotClient.Receive);if(cancel.acted)bot.SaveIntoDatabase();if(Lifecycle36.ForgeBusy(bot))return;}
    if(room.GetPlayers().Count>=4)return;
    if(room.Game!=null){var pve=room.Game as PVEGame;if(pve==null||!pve.CanAddPlayer())return;}else{if(room.IsPlaying)return;if(!room.CanAddPlayer())OpenBotSeats(room.Host,room.GetPlayers().Count+1);}
    if(bot.IsInWorldBossRoom&&!BotWorldBoss36.BeforeOffline(bot))return;
    RoomMgr.EnterRoom(bot,roomId,room.Password,(int)room.RoomType,true);
    for(int attempt=0;attempt<25&&bot.CurrentRoom!=room;attempt++)Thread.Sleep(100);
    if(bot.CurrentRoom==room){RoomMgr.UpdatePlayerState(bot,1);MissionReady36.Tick(bot);NotifySocial(bot,host,"room_invitation_accepted","玩家邀请进入房间，已进入房间并准备。","我已进房并准备好了，一起开始吧。");Console.WriteLine("Bot joined invited room: "+bot.PlayerId+" -> "+roomId);}else Console.WriteLine("BOT INVITE FAILED: "+bot.PlayerId+" -> "+roomId);
   }}catch(Exception e){Console.WriteLine("BOT INVITE: "+e.Message);}finally{if(bot.CurrentRoom==room&&room.GetPlayers().Contains(bot)){Invitation old;Invitations.TryRemove(bot.PlayerId,out old);}bool unused;InviteReservations.TryRemove(bot.PlayerId,out unused);}});
  }
  public static void OnPacket(BotPlayer bot,GSPacketIn original){if(bot==null||original==null||original.Buffer==null)return;try{var p=new GSPacketIn((byte[])original.Buffer.Clone(),original.Length);p.Code=original.Code;p.ClientID=original.ClientID;p.Parameter1=original.Parameter1;p.Parameter2=original.Parameter2;p.Offset=GSPacketIn.HDR_SIZE;
   BotWorldBoss36.OnPacket(bot,original);
   SceneInteraction36.HandleInvitation(bot,original,bot.BotClient.Receive);
   if(p.Code==(short)ePackageType.GAME_INVITE){int inviter=p.ReadInt();int roomId=p.ReadInt();var host=WorldMgr.GetPlayerById(inviter);if(host!=null){RequestRoomInvitation(bot.PlayerId,host);}}
   else if(p.Code==(short)ePackageType.SYS_MESSAGE){Console.WriteLine("BOT MESSAGE "+bot.PlayerId+": "+BitConverter.ToString(p.Buffer,GSPacketIn.HDR_SIZE,p.Length-GSPacketIn.HDR_SIZE));}
   else if(p.Code==(short)ePackageType.GAME_CMD){byte command=p.ReadByte();if(command==6)NativePveNextTurn(bot,p.ClientID);if(command==112||command==115)NativePveFinish(bot);if(command==(byte)eTankCmdType.GAME_LOAD||command==(byte)eTankCmdType.GAME_CREATE||command==(byte)eTankCmdType.GAME_MISSION_PREPARE){ThreadPool.QueueUserWorkItem(_=>{try{Thread.Sleep(300);if(command==(byte)eTankCmdType.GAME_MISSION_PREPARE){MissionReady36.Tick(bot);return;}var ready=new GSPacketIn((short)ePackageType.GAME_CMD,bot.PlayerId);ready.WriteByte(16);ready.WriteInt(100);ready.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(ready);}catch(Exception e){Console.WriteLine("BOT LOAD ACK: "+e.GetBaseException().Message);}});}}
    else if(p.Code==(short)ePackageType.SCENE_CHAT){p.ReadInt();int channel=p.ReadByte();if(p.ReadBoolean())channel=4;p.ReadString();string text=p.ReadString();var human=WorldMgr.GetPlayerById(p.ClientID);if(human==null||IsBot(human.PlayerId)||!SameScene(bot,human,channel))return;
     bool mentioned=text.Contains(bot.PlayerCharacter.NickName);if(!mentioned){var candidates=Bots.Values.Where(x=>SameScene(x,human,channel)).OrderBy(x=>x.PlayerId).ToArray();if(candidates.Length==0||candidates[human.PlayerId%candidates.Length]!=bot)return;}
     string scene=SceneDescription(human);int replyChannel=channel==0||channel==1?5:channel;
     if(Llm36.Submit(bot.PlayerId,"chat",scene+"："+bot.PlayerCharacter.NickName+"（"+bot.PlayerCharacter.Grade+"级）收到玩家"+human.PlayerCharacter.NickName+"的消息："+text,"player/"+human.PlayerId,llmAnswer=>{if(bot.Online&&WorldMgr.GetPlayerById(human.PlayerId)!=null&&SameScene(bot,human,channel))Speak(bot,llmAnswer,channel==0||channel==1?human:null,replyChannel);},()=>{if(bot.Online&&WorldMgr.GetPlayerById(human.PlayerId)!=null&&SameScene(bot,human,channel))Speak(bot,Llm36.FailureReply(bot.PlayerId),human,replyChannel);}))return;
    string key=human.PlayerId+"/"+bot.PlayerId;lock(Replies){DateTime last;if(Replies.TryGetValue(key,out last)&&(DateTime.UtcNow-last).TotalSeconds<12)return;Replies[key]=DateTime.UtcNow;}
    string answer=text.Contains("副本")||text.Contains("邀请")?"直接邀请我进副本房间，我会自动准备并攻击怪物。":text.Contains("师")||text.Contains("徒")?"可以向符合等级的我发送师徒申请，我会自动同意。":text.Contains("结婚")||text.Contains("求婚")?"向未婚异性的伙伴发送求婚申请，我会自动同意。":text.Contains("对战")||text.Contains("匹配")?"开始竞技匹配后稍等，我会参与匹配。":text.Contains("好友")?"搜索我的名字添加好友，我会自动回加。":"你好呀，有副本可以邀请我一起玩。";
     ThreadPool.QueueUserWorkItem(_=>{try{Thread.Sleep(900);if(SameScene(bot,human,channel))Speak(bot,answer,channel==0||channel==1?human:null,replyChannel);}catch(Exception e){Console.WriteLine("BOT CHAT: "+e.Message);}});
   }
   else if(p.Code==(short)ePackageType.CHAT_PERSONAL){int from=p.ClientID;int target=p.ReadInt();p.ReadString();p.ReadString();string text=p.ReadString();var human=WorldMgr.GetPlayerById(from);if(human==null||IsBot(from)||target!=bot.PlayerId)return;
    if(Llm36.Submit(bot.PlayerId,"private_chat",bot.PlayerCharacter.NickName+"收到玩家"+human.PlayerCharacter.NickName+"的私聊："+text,"player/"+from,answer=>{if(bot.Online&&WorldMgr.GetPlayerById(from)!=null)SendPersonal(bot,human,answer);},()=>{if(bot.Online&&WorldMgr.GetPlayerById(from)!=null)SendPersonal(bot,human,Llm36.FailureReply(bot.PlayerId));}))return;
    string key="pm/"+from+"/"+bot.PlayerId;lock(Replies){DateTime last;if(Replies.TryGetValue(key,out last)&&(DateTime.UtcNow-last).TotalSeconds<5)return;Replies[key]=DateTime.UtcNow;}
    SendPersonal(bot,human,"你好呀！加好友或邀请我进房间就能一起玩。");
   }
  }catch(Exception e){Console.WriteLine("BOT PACKET "+original.Code+": "+e.Message);}}
  static void SendPersonal(BotPlayer bot,GamePlayer human,string text){var reply=new GSPacketIn((short)ePackageType.CHAT_PERSONAL,bot.PlayerId);reply.WriteInt(human.PlayerId);reply.WriteString(human.PlayerCharacter.NickName);reply.WriteString(bot.PlayerCharacter.NickName);reply.WriteString(text);reply.WriteBoolean(false);human.Out.SendTCP(reply);}
  static bool SameScene(GamePlayer bot,GamePlayer human,int channel){
   if(bot.PlayerCharacter.AreaID!=human.PlayerCharacter.AreaID)return false;
   if(channel==0||channel==1)return true;
   if(channel==3)return bot.PlayerCharacter.ConsortiaID>0&&bot.PlayerCharacter.ConsortiaID==human.PlayerCharacter.ConsortiaID;
   if(bot.CurrentSpaRoom!=null||human.CurrentSpaRoom!=null)return bot.CurrentSpaRoom!=null&&Object.ReferenceEquals(bot.CurrentSpaRoom,human.CurrentSpaRoom);
   if(bot.CurrentMarryRoom!=null||human.CurrentMarryRoom!=null)return bot.CurrentMarryRoom!=null&&Object.ReferenceEquals(bot.CurrentMarryRoom,human.CurrentMarryRoom)&&bot.MarryMap==human.MarryMap;
   if(bot.CurrentRoom!=null||human.CurrentRoom!=null)return bot.CurrentRoom!=null&&Object.ReferenceEquals(bot.CurrentRoom,human.CurrentRoom)&&(channel!=4||bot.CurrentRoomTeam==human.CurrentRoomTeam);
   if(bot.IsInWorldBossRoom||human.IsInWorldBossRoom)return bot.IsInWorldBossRoom&&human.IsInWorldBossRoom;
   return channel!=9&&channel!=13&&channel!=4;
  }
  static string SceneDescription(GamePlayer player){return player.CurrentSpaRoom!=null?"温泉房间"+player.CurrentSpaRoom.Spa_Room_Info.RoomID:player.CurrentMarryRoom!=null?"婚礼礼堂"+player.CurrentMarryRoom.Info.ID+"场景"+player.MarryMap:player.CurrentRoom!=null?"游戏房间"+player.CurrentRoom.RoomId:player.IsInWorldBossRoom?"世界Boss大厅":"游戏大厅";}
  public static void Speak(BotPlayer bot,string text,GamePlayer only,int channel){var p=new GSPacketIn((short)ePackageType.SCENE_CHAT,bot.PlayerId);p.WriteInt(bot.PlayerCharacter.AreaID);p.WriteByte((byte)channel);p.WriteBoolean(channel==4);p.WriteString(bot.PlayerCharacter.NickName);p.WriteString(text);
   if(only!=null)only.Out.SendTCP(p);else foreach(var human in WorldMgr.GetAllPlayers().Where(x=>!IsBot(x.PlayerId)&&!x.IsRobot&&SameScene(bot,x,channel)))human.Out.SendTCP(p);
  }
  static object CanonicalJsonValue(object value){var map=value as IDictionary<string,object>;if(map!=null){var sorted=new SortedDictionary<string,object>(StringComparer.Ordinal);foreach(var entry in map)sorted.Add(entry.Key,CanonicalJsonValue(entry.Value));return sorted;}var array=value as object[];if(array!=null)return array.Select(CanonicalJsonValue).ToArray();var list=value as ArrayList;if(list!=null)return list.Cast<object>().Select(CanonicalJsonValue).ToArray();return value;}
  static object ProgressionAdmin(HttpListenerContext ctx,bool save){
   var json=new JavaScriptSerializer();GMProgressionRequest36 request=null;int id;
   if(save){if(ctx.Request.HttpMethod!="POST"||ctx.Request.ContentLength64>32768)throw new Exception("Invalid progression request");string raw;using(var reader=new StreamReader(ctx.Request.InputStream))raw=reader.ReadToEnd();if(raw.Length>32768)throw new Exception("Request too large");request=json.Deserialize<GMProgressionRequest36>(raw);if(request==null)throw new ArgumentException("修改内容为空");id=request.userId;}else id=Int32.Parse(ctx.Request.QueryString["id"]??"0");
   if(!Roster.ContainsKey(id))throw new ArgumentException("请选择名册中的人机");
   lock(BotAdminGate){BotPlayer player;bool temporary=!Bots.TryGetValue(id,out player);if(temporary){using(var db=new PlayerBussiness()){var info=db.GetUserSingleByUserID(id);if(info==null)throw new InvalidOperationException("角色不存在");player=new BotPlayer(info,new BotClient());if(!player.LoadFromDatabase())throw new InvalidOperationException("原生角色加载失败");player.Online=false;player.PlayerCharacter.State=0;InitializeCombatProfile(player);}}
    try{lock(player){if(!save)return new{ok=true,online=!temporary,profile=GMProgression36.Inspect(player)};Guid requestId;if(!Guid.TryParse(request.requestId,out requestId)||requestId==Guid.Empty)throw new ArgumentException("修改请求编号无效");request.requestId=requestId.ToString();string payload=json.Serialize(CanonicalJsonValue(json.DeserializeObject(json.Serialize(request)))),before=json.Serialize(GMProgression36.Inspect(player));string duplicate=ProgressionAudit36.Begin(requestId,id,payload,before);if(duplicate!=null)return json.DeserializeObject(duplicate);
     try{var changes=GMProgression36.Apply(player,request);if(temporary){player.PlayerCharacter.State=0;if(!player.SaveIntoDatabase())throw new InvalidOperationException("离线角色原生保存失败");}string result=json.Serialize(new{ok=changes.ok,online=!temporary,requestId=requestId.ToString(),changes=changes,message=changes.ok?"原生装备与系统属性已保存，可在审计中查看修改前后记录":"修改未保存，请查看回滚与错误信息"});ProgressionAudit36.Complete(requestId,result,changes.ok?null:changes.error??"原生修改失败");if(changes.ok){BotLifecycleDriver36.Invalidate(id);RankSync36.MarkDirty();}return json.DeserializeObject(result);}catch(Exception e){ProgressionAudit36.Complete(requestId,null,e.GetBaseException().Message);throw;}
    }}finally{if(temporary){player.Online=false;player.PlayerCharacter.State=0;if(!player.SaveIntoDatabase())throw new InvalidOperationException("离线角色状态恢复失败，请检查服务日志");}}
   }
  }
  static void StartAdmin(){var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:9440/");listener.Start();var thread=new Thread(()=>{while(true){var context=listener.GetContext();ThreadPool.QueueUserWorkItem(_=>HandleAdmin(context));}});thread.IsBackground=true;thread.Start();}
  static int TestId(HttpListenerContext ctx,string key,int fallback){string raw=ctx.Request.QueryString[key];return String.IsNullOrEmpty(raw)?fallback:Int32.Parse(raw);}
  static void HandleAdmin(HttpListenerContext ctx){try{string secret=File.ReadAllText(Path.Combine(Root,"config","runtime-token.txt")).Trim();if(ctx.Request.Headers["X-DDT-Token"]!=secret){ctx.Response.StatusCode=403;return;}
   object result=null;string path=ctx.Request.Url.AbsolutePath;
   if(path=="/status")result=new{shutdown=ShuttingDown,bots=Roster.Values.OrderBy(p=>p.UserID).Select(p=>{BotPlayer active;bool loaded=Bots.TryGetValue(p.UserID,out active);return new{id=p.UserID,name=p.Name,grade=loaded?active.PlayerCharacter.Grade:p.Level,area=loaded?active.PlayerCharacter.AreaID:0,power=loaded?active.PlayerCharacter.FightPower:0,nimbus=loaded?active.PlayerCharacter.Nimbus:0,online=loaded&&active.Online&&WorldMgr.GetPlayerById(p.UserID)==active,registered=loaded&&WorldMgr.GetPlayerById(p.UserID)==active,room=loaded&&active.CurrentRoom!=null?active.CurrentRoom.RoomId:0,retiring=loaded&&!desiredOnline.Contains(p.UserID)};}).ToArray(),pool=new{capacity=50,total=Roster.Count,online=Bots.Count,desired=desiredOnline,nextRotationUtc=pool.State.NextRotationUtc,generation=pool.State.Generation},llm=Llm36.Status(),worldBoss=BotWorldBoss36.Status(),players=WorldMgr.GetAllPlayers().Length};
    else if(path=="/prepare-stop"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");ShuttingDown=true;bool ready=true;var humans=WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot).ToArray();foreach(var bot in Bots.Values){if(Lifecycle36.ForgeBusy(bot)&&bot.CurrentRoom==null){lock(bot){var step=Lifecycle36.CancelForge(bot,bot.BotClient.Receive);if(step.acted)bot.SaveIntoDatabase();if(Lifecycle36.ForgeBusy(bot))ready=false;}}if(!Departure36.Ready(bot,humans,()=>bot.Online&&ShuttingDown,(actor,human,text)=>SendPersonal((BotPlayer)actor,human,text),true))ready=false;}result=new{ok=true,ready=ready};}
    else if(path=="/rooms/status")result=WorldMgr.GetAllPlayers().Where(p=>!IsBot(p.PlayerId)&&!p.IsRobot).Select(p=>new{id=p.PlayerId,name=p.PlayerCharacter.NickName,grade=p.PlayerCharacter.Grade,power=p.PlayerCharacter.FightPower,room=p.CurrentRoom==null?null:(object)new{id=p.CurrentRoom.RoomId,type=p.CurrentRoom.RoomType.ToString(),playing=p.CurrentRoom.IsPlaying,hasGame=p.CurrentRoom.Game!=null,capacity=p.CurrentRoom.PlacesCount,canAdd=p.CurrentRoom.CanAddPlayer(),players=p.CurrentRoom.GetPlayers().Select(x=>new{id=x.PlayerId,bot=IsBot(x.PlayerId),grade=x.PlayerCharacter.Grade,power=x.PlayerCharacter.FightPower}).ToArray()}}).ToArray();
    else if(path=="/match-status")result=MatchStatus.Select(pair=>new{room=pair.Key,detail=pair.Value}).ToArray();
    else if(path=="/resume"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");ShuttingDown=false;Departure36.Reset();result=new{ok=true};}
    else if(path=="/reload"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");Interlocked.Exchange(ref reload,1);result=new{ok=true};}
    else if(path=="/lifecycle/invalidate"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");int id=TestId(ctx,"id",0);if(!Roster.ContainsKey(id))throw new ArgumentException("Unknown bot");BotLifecycleDriver36.Invalidate(id);result=new{ok=true};}
    else if(path=="/gm/progression")result=ProgressionAdmin(ctx,false);
    else if(path=="/gm/progression-save")result=ProgressionAdmin(ctx,true);
    else if(path=="/gm/native-growth")result=NativeGrowthAdmin(ctx,false);
    else if(path=="/gm/native-growth-step")result=NativeGrowthAdmin(ctx,true);
    else if(path=="/gm/llm-test"){if(ctx.Request.HttpMethod!="POST")throw new ArgumentException("POST required");int id=TestId(ctx,"id",0);if(!Roster.ContainsKey(id))throw new ArgumentException("Unknown bot");result=Llm36.Probe(id);}
   else if(path=="/llm/battle"){if(ctx.Request.HttpMethod!="POST"||ctx.Request.ContentLength64>16384)throw new Exception("Invalid request");string body;using(var reader=new StreamReader(ctx.Request.InputStream))body=reader.ReadToEnd();if(body.Length>16000)throw new Exception("Request too large");var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);BattleReply(Convert.ToInt32(data["id"]),Convert.ToString(data["game"]),Convert.ToInt32(data["turn"]),Convert.ToString(data["summary"]));result=new{ok=true};}
   else if(path.StartsWith("/test-")&&!File.Exists(Path.Combine(Root,"config","testing.flag"))){ctx.Response.StatusCode=404;return;}
   else if(path=="/notify-mail"){int id=Int32.Parse(ctx.Request.QueryString["id"]);var player=WorldMgr.GetPlayerById(id);if(player!=null)player.Out.SendMailResponse(id,Game.Server.Packets.eMailRespose.Receiver);result=new{ok=true};}
   else if(path=="/test-room-control"||path=="/test-room-state")result=BotAcceptance36.Handle(ctx,id=>Bots[id],id=>Roster.ContainsKey(id),OpenBotSeats);
   else if(path=="/test-pool-rotate"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");Interlocked.Exchange(ref forceRotation,1);result=new{ok=true};}
   else if(path=="/test-pool-promote"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");var ids=(ctx.Request.QueryString["ids"]??"").Split(',').Select(Int32.Parse).Distinct().ToArray();if(ids.Length<1||ids.Length>4||ids.Any(id=>!Roster.ContainsKey(id)||!Roster[id].Enabled))throw new Exception("Use enabled roster fixture bots only");lock(BotAdminGate){bool ok=pool.Promote(ids,Bots.Values.Where(MatchIdle).Select(p=>p.PlayerId));if(ok){desiredOnline=pool.State.DesiredIds.ToArray();foreach(int id in ids)MatchPins[id]=DateTime.UtcNow.AddMinutes(10);}result=new{ok=ok};}}
   else if(path=="/test-invite"){int botId=Int32.Parse(ctx.Request.QueryString["bot"]);int humanId=Int32.Parse(ctx.Request.QueryString["human"]);var b=Bots[botId];var h=WorldMgr.GetPlayerById(humanId);if(h==null||h.CurrentRoom==null)throw new Exception("Player must have a room");RoomMgr.EnterRoom(b,h.CurrentRoom.RoomId,h.CurrentRoom.Password,(int)h.CurrentRoom.RoomType,true);result=new{ok=true};}
   else if(path=="/test-claim-mail"){int id=Int32.Parse(ctx.Request.QueryString["bot"]);int mail=Int32.Parse(ctx.Request.QueryString["mail"]);var b=Bots[id];int money=b.PlayerCharacter.Money,gold=b.PlayerCharacter.Gold,gift=b.PlayerCharacter.GiftToken;var p=new GSPacketIn((short)ePackageType.GET_MAIL_ATTACHMENT,id);p.WriteInt(mail);p.WriteByte(0);p.Offset=GSPacketIn.HDR_SIZE;b.BotClient.Receive(p);b.SaveIntoDatabase();result=new{ok=true,before=new{money,gold,gift},after=new{money=b.PlayerCharacter.Money,gold=b.PlayerCharacter.Gold,gift=b.PlayerCharacter.GiftToken}};}
    else if(path=="/test-social-event"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");var human=Bots[TestId(ctx,"human",5)];var bot=Bots[TestId(ctx,"bot",6)];string kind=ctx.Request.QueryString["event"]??"friend_added";if(!new[]{"friend_added","marriage_accepted","academy_accepted","room_invitation_accepted"}.Contains(kind))throw new Exception("Unknown social event");int seen=0;human.BotClient.PacketObserver=packet=>{if(packet.Code==37&&packet.ClientID==bot.PlayerId)Interlocked.Increment(ref seen);};lock(TestHumans)TestHumans.Add(human.PlayerId);try{NotifySocial(bot,human,kind,"本机验收：该项互动已由测试事件确认。","事件固定主动回复验收");for(int attempt=0;attempt<25&&Interlocked.CompareExchange(ref seen,0,0)==0;attempt++)Thread.Sleep(100);result=new{ok=seen>0,personalReplies=seen,eventName=kind};}finally{lock(TestHumans)TestHumans.Remove(human.PlayerId);human.BotClient.PacketObserver=null;}}
    else if(path=="/test-match-native-build"){throw new Exception("Player-copy matchmaking has been removed; use existing lobby bots");}
    else if(path=="/test-lifecycle-inspect")result=Lifecycle36.Inspect(Bots[TestId(ctx,"bot",5)]);
    else if(path=="/test-lifecycle-step"){if(ctx.Request.HttpMethod!="POST"||ctx.Request.ContentLength64>4096)throw new Exception("Invalid QA request");var bot=Bots[TestId(ctx,"bot",5)];string raw;using(var reader=new StreamReader(ctx.Request.InputStream))raw=reader.ReadToEnd();if(raw.Length>4096)throw new Exception("Request too large");var selection=new JavaScriptSerializer().Deserialize<LifecycleAction36>(raw);lock(bot){result=Lifecycle36.Execute(bot,selection,Math.Min(15,Math.Max(0,TestId(ctx,"maxStrengthen",6))),bot.BotClient.Receive);bot.SaveIntoDatabase();}}
    else if(path=="/test-mailcare-inspect")result=MailCare36.InspectInventory(Bots[TestId(ctx,"bot",5)]);
    else if(path=="/test-profile-recalculate"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=NativeProfile36.Trace(Bots[TestId(ctx,"bot",5)]);}
    else if(path=="/test-profile-connected"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=NativeProfile36.TraceConnected(Bots[TestId(ctx,"bot",5)]);}
    else if(path=="/test-profile-aggregation"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=NativeProfile36.TraceAggregation(Bots[TestId(ctx,"bot",5)]);}
    else if(path=="/test-profile-transactions"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=NativeProfile36.TraceTransactions(Bots[TestId(ctx,"bot",5)]);}
   else if(path=="/test-player-profile"){var player=WorldMgr.GetPlayerById(TestId(ctx,"user",1));if(player==null)throw new Exception("Player is offline; read-only endpoint does not load or save users");lock(player){result=NativeProfile36.Snapshot(player);}}
   else if(path=="/test-native-stats"){var bot=Bots[TestId(ctx,"bot",5)];lock(bot){if(ctx.Request.HttpMethod=="POST"){if(bot.CurrentRoom!=null||bot.IsInWorldBossRoom||Lifecycle36.ForgeBusy(bot))throw new Exception("Bot must be idle");result=NativeStats36.Recalculate(bot);}else result=NativeStats36.Describe(bot);}}
   else if(path=="/test-profile-native-connect"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=NativeProfile36.TraceNativeConnect(Bots[TestId(ctx,"bot",5)]);}
   else if(path=="/test-scene-state")result=SceneInteraction36.InspectScene(Bots[TestId(ctx,"bot",5)]);
    else if(path=="/test-scene-leave"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");result=SceneInteraction36.LeaveNativeScenes(Bots[TestId(ctx,"bot",5)]);}
    else if(path=="/test-tactics")result=LastTactics.ToDictionary(pair=>pair.Key.ToString(),pair=>pair.Value);
    else if(path=="/test-scene-chat"){var human=Bots[TestId(ctx,"human",21)];var bot=Bots[TestId(ctx,"bot",22)];string scene=ctx.Request.QueryString["scene"]??"room";object chat=null;Action<GamePlayer,GamePlayer> probe=(h,b)=>{chat=SceneInteraction36.ProbeSceneChat(h,b,scene=="spa"?13:scene=="marry"?9:5,ctx.Request.QueryString["text"]??"remember: scene interaction QA",human.BotClient.Receive,observer=>human.BotClient.PacketObserver=observer);};lock(TestHumans)TestHumans.Add(human.PlayerId);try{object fixture=scene=="spa"?SceneInteraction36.CaptureNativeSpaInvite(human,bot,human.BotClient.Receive,observer=>bot.BotClient.PacketObserver=observer,probe):scene=="marry"?SceneInteraction36.CaptureNativeMarryInvite(human,bot,human.BotClient.Receive,observer=>bot.BotClient.PacketObserver=observer,probe):SceneInteraction36.WithNativeRoom(human,bot,probe);result=new{fixture=fixture,chat=chat};}finally{lock(TestHumans)TestHumans.Remove(human.PlayerId);}}
    else if(path=="/test-spa-capture"){var host=Bots[TestId(ctx,"host",21)];var guest=Bots[TestId(ctx,"guest",22)];result=SceneInteraction36.CaptureNativeSpaInvite(host,guest,host.BotClient.Receive,observer=>guest.BotClient.PacketObserver=observer);}
    else if(path=="/test-presence-view"){var viewer=Bots[TestId(ctx,"viewer",113)];var target=Bots[TestId(ctx,"bot",112)];lock(TestHumans)TestHumans.Add(viewer.PlayerId);try{result=Presence36.Verify(viewer,target,viewer.BotClient.Receive,observer=>viewer.BotClient.PacketObserver=observer);}finally{lock(TestHumans)TestHumans.Remove(viewer.PlayerId);}}
    else if(path=="/test-presence")result=Presence36.Describe(Bots.Values);
    else if(path=="/test-appearance"){var actor=Bots[TestId(ctx,"bot",125)];result=new{appearance=BotAppearance36.Inspect(actor),area=actor.PlayerCharacter.AreaID,power=actor.PlayerCharacter.FightPower,grade=actor.PlayerCharacter.Grade};}
    else if(path=="/test-match-party"){
     if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");int[] ids=(ctx.Request.QueryString["ids"]??"").Split(',').Select(Int32.Parse).ToArray();if(ids.Length<1||ids.Length>4||ids.Distinct().Count()!=ids.Length)throw new Exception("Use one to four distinct test members");
     var party=ids.Select(id=>Bots[id]).ToArray();if(party.Any(p=>p.CurrentRoom!=null||p.CurrentSpaRoom!=null||p.CurrentMarryRoom!=null))throw new Exception("Test party busy");var host=party[0];lock(TestHumans)TestHumans.Add(host.PlayerId);
     RoomMgr.CreateRoom(host,"整队匹配验证","",eRoomType.Match,20);
     ThreadPool.QueueUserWorkItem(_=>{try{for(int i=0;i<20&&host.CurrentRoom==null;i++)Thread.Sleep(150);if(host.CurrentRoom==null)throw new Exception("No test room");OpenBotSeats(host,party.Length);foreach(var member in party.Skip(1))RoomMgr.EnterRoom(member,host.CurrentRoom.RoomId,host.CurrentRoom.Password,(int)eRoomType.Match,true);for(int i=0;i<20&&host.CurrentRoom.GetPlayers().Count!=party.Length;i++)Thread.Sleep(150);if(host.CurrentRoom.GetPlayers().Count!=party.Length)throw new Exception("Incomplete native test party; refusing to start");foreach(var member in party)RoomMgr.UpdatePlayerState(member,1);var start=new GSPacketIn(94,host.PlayerId);start.WriteInt(7);start.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(start);Thread.Sleep(90000);}catch(Exception e){Console.WriteLine("PARTY QA: "+e.Message);}finally{lock(TestHumans)TestHumans.Remove(host.PlayerId);}});result=new{ok=true,party=ids};
    }
    else if(path=="/test-pve-party"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");int[] ids=(ctx.Request.QueryString["ids"]??"").Split(',').Select(Int32.Parse).ToArray();if(ids.Length<1||ids.Length>4||ids.Distinct().Count()!=ids.Length)throw new Exception("Use distinct all-bot fixture IDs");var party=ids.Select(id=>Bots[id]).ToArray();if(party.Any(p=>!SceneInteraction36.IsIdle(p)))throw new Exception("Fixture bots must be idle");var host=party[0];RoomMgr.CreateRoom(host,"四人副本验收","",eRoomType.Dungeon,20);for(int i=0;i<20&&host.CurrentRoom==null;i++)Thread.Sleep(100);var room=host.CurrentRoom;if(room==null)throw new Exception("Native room creation timed out");room.MapId=1;room.HardLevel=eHardLevel.Simple;room.LevelLimits=0;room.UpdateRoomGameType();OpenBotSeats(host,party.Length);foreach(var member in party.Skip(1))RoomMgr.EnterRoom(member,room.RoomId,room.Password,(int)eRoomType.Dungeon,true);for(int i=0;i<30&&room.GetPlayers().Count!=party.Length;i++)Thread.Sleep(100);if(room.GetPlayers().Count!=party.Length)throw new Exception("Incomplete native PVE party; refusing to start: count="+room.GetPlayers().Count+" capacity="+room.PlacesCount);foreach(var member in party)RoomMgr.UpdatePlayerState(member,1);RoomMgr.StartGame(room);result=new{ok=true,room=room.RoomId,players=room.GetPlayers().Count,capacity=room.PlacesCount};}
    else if(path=="/test-pvp"){var a=Bots[TestId(ctx,"a",13)];var b=Bots[TestId(ctx,"b",15)];if(a.CurrentRoom!=null||b.CurrentRoom!=null)throw new Exception("Test bots are busy");RoomMgr.CreateRoom(a,"人机战斗验证甲","",eRoomType.Match,20);RoomMgr.CreateRoom(b,"人机战斗验证乙","",eRoomType.Match,20);ThreadPool.QueueUserWorkItem(_=>{Thread.Sleep(1000);RoomMgr.UpdatePlayerState(a,1);RoomMgr.UpdatePlayerState(b,1);RoomMgr.StartGame(a.CurrentRoom);RoomMgr.StartGame(b.CurrentRoom);});result=new{ok=true};}
   else if(path=="/test-academy"){var master=Bots[26];var student=Bots[13];var request=new AcademyRequestInfo{SenderID=student.PlayerId,ReceiderID=master.PlayerId,Type=(int)Game.Server.Packets.eAcademyType.ACADEMY_FOR_MASTER,CreateTime=DateTime.Now};AcademyMgr.AddRequest(request);AcceptAcademy(student);result=new{ok=AcademyMgr.GetRequest(student.PlayerId,master.PlayerId)==null,master=master.PlayerId,student=student.PlayerId,canMaster=AcademyMgr.CheckCanMaster(master.PlayerCharacter.Grade),canStudent=AcademyMgr.CheckCanApp(student.PlayerCharacter.Grade),type=request.Type};}
   else if(path=="/test-marry"){var a=Bots[17];var b=Bots[18];int id=0;using(var db=new PlayerBussiness()){bool sent=db.SavePlayerMarryNotice(new MarryApplyInfo{UserID=b.PlayerId,ApplyUserID=a.PlayerId,ApplyUserName=a.PlayerCharacter.NickName,ApplyType=1,ApplyResult=false,LoveProclamation="人机自动同意验证"},0,ref id);result=new{ok=sent,notice=id};}}
   else if(path=="/test-pve"){var host=Bots[TestId(ctx,"host",29)];var member=Bots[TestId(ctx,"member",30)];if(host.CurrentRoom!=null||member.CurrentRoom!=null)throw new Exception("Test bots busy");RoomMgr.CreateRoom(host,"人机副本验证","",eRoomType.Dungeon,20);Thread.Sleep(500);var room=host.CurrentRoom;if(room==null)throw new Exception("Room creation pending");room.MapId=1;room.HardLevel=eHardLevel.Simple;room.LevelLimits=0;room.UpdateRoomGameType();foreach(byte seat in new byte[]{2,3}){var open=new GSPacketIn(94,host.PlayerId);open.WriteInt(10);open.WriteByte(seat);open.WriteInt(-1);open.WriteBoolean(false);open.WriteInt(-100);open.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(open);}var invite=new GSPacketIn((short)ePackageType.GAME_INVITE,host.PlayerId);invite.WriteInt(host.PlayerId);invite.WriteInt(room.RoomId);invite.Offset=GSPacketIn.HDR_SIZE;OnPacket(member,invite);Thread.Sleep(1800);RoomMgr.UpdatePlayerState(host,1);RoomMgr.UpdatePlayerState(member,1);lock(TestHumans)TestHumans.Add(host.PlayerId);ThreadPool.QueueUserWorkItem(_=>{try{for(int i=0;i<600&&host.CurrentRoom==room;i++)Thread.Sleep(1000);}finally{lock(TestHumans)TestHumans.Remove(host.PlayerId);}});RoomMgr.StartGame(room);result=new{ok=true,room=room.RoomId,players=room.GetPlayers().Count};}
   else if(path=="/test-pve-control"){if(ctx.Request.HttpMethod!="POST")throw new Exception("POST required");var host=Bots[TestId(ctx,"host",49)];var room=host.CurrentRoom;if(room==null||room.Host!=host||!(room.Game is PVEGame)||room.GetPlayers().Any(p=>!Roster.ContainsKey(p.PlayerId)))throw new Exception("Only an all-bot native PVE fixture is permitted");string action=ctx.Request.QueryString["action"];if(action=="invite"){var invite=new GSPacketIn(70,host.PlayerId);invite.WriteInt(TestId(ctx,"member",47));invite.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(invite);}else if(action=="open"){foreach(byte seat in new byte[]{2,3}){var open=new GSPacketIn(94,host.PlayerId);open.WriteInt(10);open.WriteByte(seat);open.WriteInt(-1);open.WriteBoolean(false);open.WriteInt(-100);open.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(open);}}else if(action=="start"){var start=new GSPacketIn(82,host.PlayerId);start.WriteBoolean(true);start.Offset=GSPacketIn.HDR_SIZE;host.BotClient.Receive(start);}else if(action=="leave-member"){int memberId=TestId(ctx,"member",48);if(memberId==host.PlayerId)throw new Exception("Fixture host must remain in room");var member=room.GetPlayers().FirstOrDefault(p=>p.PlayerId==memberId);if(member==null)throw new Exception("Fixture member not present");RoomMgr.ExitRoom(room,member);}else if(action=="leave"){foreach(var member in room.GetPlayers().ToArray())RoomMgr.ExitRoom(room,member);}else throw new Exception("Unknown native PVE control action");result=new{ok=true};}
   else if(path=="/test-games")result=Bots.Values.Where(b=>b.CurrentRoom!=null).Select(b=>new{id=b.PlayerId,room=b.CurrentRoom.RoomId,playing=b.CurrentRoom.IsPlaying,game=b.CurrentRoom.Game==null?null:b.CurrentRoom.Game.GetType().FullName,state=b.CurrentRoom.Game==null?null:b.CurrentRoom.Game.ToString(),session=b.CurrentRoom.Game is PVEGame?((PVEGame)b.CurrentRoom.Game).SessionId:0,canAdd=b.CurrentRoom.Game is PVEGame&&((PVEGame)b.CurrentRoom.Game).CanAddPlayer(),allReady=b.CurrentRoom.Game is PVEGame&&((PVEGame)b.CurrentRoom.Game).IsAllReady(),weapon=b.MainBag.GetItemAt(6)==null?0:b.MainBag.GetItemAt(6).TemplateID,equip=b.EquipBag.GetItemAt(6)==null?0:b.EquipBag.GetItemAt(6).TemplateID,actors=b.CurrentRoom.Game is BaseGame?((BaseGame)b.CurrentRoom.Game).GetAllLivingPlayers().Select(p=>new{user=p.PlayerDetail.PlayerCharacter.ID,blood=p.Blood,maxBlood=p.MaxBlood,team=p.Team,level=p.PlayerDetail.PlayerCharacter.Grade,power=p.PlayerDetail.PlayerCharacter.FightPower,attacking=p.IsAttacking,ready=p.Ready,attack=p.PlayerDetail.PlayerCharacter.Attack,defence=p.PlayerDetail.PlayerCharacter.Defence,baseDamage=p.BaseDamage,baseGuard=p.BaseGuard}).ToArray():null,npcs=b.CurrentRoom.Game is BaseGame?((BaseGame)b.CurrentRoom.Game).FindAllNpcLiving().Select(p=>new{id=p.Id,blood=p.Blood,maxBlood=p.MaxBlood}).ToArray():null}).ToArray();
   else if(path=="/test-inventory"){var bot=Bots[29];bot.PlayerCharacter.IsFirst=4;bot.MainBag.LoadFromDatabase();bot.MainBag.UpdatePlayerProperties();InitializeCombatProfile(bot);bot.UpdateProperties();bot.UpdateWeapon(bot.MainBag.GetItemAt(6));result=new{weapon=bot.MainBag.GetItemAt(6)==null?0:bot.MainBag.GetItemAt(6).TemplateID,equip=bot.EquipBag.GetItemAt(6)==null?0:bot.EquipBag.GetItemAt(6).TemplateID,attack=bot.PlayerCharacter.Attack,blood=bot.PlayerCharacter.Blood};}
   else if(path=="/test-chat"){var human=Bots[TestId(ctx,"human",9)];var bot=Bots[TestId(ctx,"bot",10)];var seen=new List<Dictionary<string,object>>();human.BotClient.PacketObserver=packet=>{if(packet.ClientID==bot.PlayerId){lock(seen)seen.Add(new Dictionary<string,object>{{"code",packet.Code},{"sender",packet.ClientID},{"length",packet.Length}});}};
    lock(TestHumans)TestHumans.Add(human.PlayerId);try{var pm=new GSPacketIn((short)ePackageType.CHAT_PERSONAL,human.PlayerId);pm.WriteInt(bot.PlayerId);pm.WriteString(bot.PlayerCharacter.NickName);pm.WriteString(human.PlayerCharacter.NickName);pm.WriteString(ctx.Request.QueryString["text"]??"你好");pm.Offset=GSPacketIn.HDR_SIZE;human.BotClient.Receive(pm);Speak(bot,"大厅消息验证",null,0);Thread.Sleep(200);lock(seen)result=new{ok=seen.Any(x=>Convert.ToInt32(x["code"])==37)&&seen.Any(x=>Convert.ToInt32(x["code"])==19),packets=seen.ToArray()};}finally{lock(TestHumans)TestHumans.Remove(human.PlayerId);human.BotClient.PacketObserver=null;}
   }
   else if(path=="/test-il"){var asm=typeof(GamePlayer).Assembly;string typeName=ctx.Request.QueryString["type"]??"Game.Logic.Phy.Object.Living";if(typeName.IndexOf('.')<0)typeName="Game.Logic.Phy.Object."+typeName;var targetType=asm.GetType(typeName,false);var sb=new StringBuilder();if(targetType==null){sb.AppendLine("type not found: "+typeName+" (asm "+asm.GetName().Name+")");}else{sb.AppendLine(IlDump36.DumpMembers(targetType));string methodName=ctx.Request.QueryString["method"];if(!string.IsNullOrEmpty(methodName)){foreach(MethodInfo mi in targetType.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))if(mi.Name==methodName)sb.AppendLine(IlDump36.Dump(mi));}}result=new{il=sb.ToString()};}
   else{ctx.Response.StatusCode=404;return;}
   byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(result));ctx.Response.ContentType="application/json; charset=utf-8";ctx.Response.OutputStream.Write(bytes,0,bytes.Length);
  }catch(Exception e){Console.WriteLine("ADMIN REQUEST FAILED: "+e);ctx.Response.StatusCode=500;byte[] b=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{error=e.Message}));ctx.Response.OutputStream.Write(b,0,b.Length);}finally{ctx.Response.Close();}}
 }
}







