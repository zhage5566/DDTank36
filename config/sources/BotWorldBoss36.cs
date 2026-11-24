using System;
using System.Collections.Generic;
using System.Linq;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.Rooms;
using Game.Server.GameRoom.Handle;
using Game.Server.Managers;
using Game.Logic.Phy.Object;
using System.Reflection;

namespace Local36 {
 // Uses the shipped 3.6 client's packets. Activity, admission, tickets, cooldown,
 // damage, rankings and rewards remain owned by the native server handlers.
 public static class BotWorldBoss36 {
  sealed class Visit {
   public int Stage,Attempts,Starts; public bool Allowed; public DateTime Next,Deadline;
   public long Damage; public string Last="waiting",NativeCommand="";
  }
  static readonly object Gate=new object();
  static readonly Dictionary<int,Visit> Visits=new Dictionary<int,Visit>();
  static string lastError="";static DateTime nextAdmission=DateTime.MinValue;
  sealed class SeenGame {public int Session;public DateTime Last;public readonly Dictionary<int,Living> Enemies=new Dictionary<int,Living>();}
  static readonly object CombatGate=new object();static readonly Dictionary<PVEGame,SeenGame> Seen=new Dictionary<PVEGame,SeenGame>();
  // Observe engine-owned objects only. World bosses can exist exclusively in
  // TurnQueue/CurrentTurnLiving, outside BaseGame's ordinary NPC collections.
  public static void Observe(PVEGame game){if(game==null)return;lock(CombatGate){
   SeenGame seen;if(!Seen.TryGetValue(game,out seen)||seen.Session!=game.SessionId)Seen[game]=seen=new SeenGame{Session=game.SessionId};seen.Last=DateTime.UtcNow;
   var queue=game.TurnQueue;var values=new List<Living>();if(queue!=null)values.AddRange(queue.ToArray());values.Add(game.CurrentTurnLiving);values.Add(game.CurrentLiving);values.Add(game.LastTurnLiving);
   foreach(var living in values)if(living!=null&&(living is SimpleBoss||living is SimpleNpc))seen.Enemies[living.Id]=living;
   if(Seen.Count>128)foreach(var old in Seen.OrderBy(x=>x.Value.Last).Take(32).Select(x=>x.Key).ToArray())Seen.Remove(old);
  }}
  public static Living[] Targets(PVEGame game,Player actor){Observe(game);lock(CombatGate){SeenGame seen;if(actor==null||!Seen.TryGetValue(game,out seen))return new Living[0];return seen.Enemies.Values.Where(p=>p.IsLiving&&p.Team!=actor.Team).OrderByDescending(p=>p.Config!=null&&p.Config.IsWorldBoss).ThenBy(p=>Math.Abs(p.X-actor.X)).ToArray();}}
  static Visit Get(BotPlayer bot) {Visit v;if(!Visits.TryGetValue(bot.PlayerId,out v))Visits[bot.PlayerId]=v=new Visit{Next=DateTime.UtcNow.AddSeconds(5+(bot.PlayerId%25)*3)};return v;}
  static bool Open(BaseWorldBossRoom boss) {return boss!=null&&boss.WorldOpen&&!boss.FightOver&&!boss.RoomClose&&!boss.IsDie&&boss.Blood>0&&boss.CurrentPVE>0&&DateTime.Now>=boss.BeginTime&&DateTime.Now<boss.EndTime;}
  static GSPacketIn Packet(BotPlayer bot,int code) {return new GSPacketIn((short)code,bot.PlayerId);}
  static void Send(BotPlayer bot,GSPacketIn packet) {
   packet.Offset=GSPacketIn.HDR_SIZE;
   if(packet.Code!=94){bot.BotClient.Receive(packet);return;}
   // Native 94 uses a second game-room processor queue in GamePlayer. Bots have
   // no connected socket; dispatch to its original command implementation.
   // These handlers retain the native activity, ticket and permission checks.
   packet.WriteHeader();packet.Offset=GSPacketIn.HDR_SIZE;int command=packet.ReadInt();IGameRoomCommandHadler handler;
   switch(command){case 0:handler=new Create();break;case 2:handler=new SetupChange();break;case 5:handler=new RemovePlayer();break;case 7:handler=new GameStart();break;default:throw new InvalidOperationException("Unsupported Boss room command "+command);}
   bool accepted=handler.CommandHandler(bot,packet);var room=bot.CurrentRoom;
   Get(bot).NativeCommand=command+":"+handler.GetType().Name+" result="+accepted+" room="+(room==null?"none":room.RoomId+"/"+room.RoomType+" map="+room.MapId);
   Console.WriteLine("BOT WORLD BOSS COMMAND: "+bot.PlayerId+" "+Get(bot).NativeCommand);
  }
  static void BossCommand(BotPlayer bot,byte command) {var p=Packet(bot,102);p.WriteByte(command);Send(bot,p);}
  static void NativeStatus(BotPlayer bot,byte status) {var p=Packet(bot,102);p.WriteByte(36);p.WriteByte(status);Send(bot,p);}
  static void ExitBattle(BotPlayer bot) {var p=Packet(bot,94);p.WriteInt(5);Send(bot,p);}
  public static bool BeforeOffline(BotPlayer bot) {lock(Gate){try{
   var room=bot.CurrentRoom;var game=room==null?null:room.Game as BaseGame;
   if(room!=null&&room.RoomType==eRoomType.WordBossFight&&room.IsPlaying&&(game==null||game.GameState!=eGameState.ALLSessionStopped))return false;
   if(room!=null&&room.RoomType==eRoomType.WordBossFight)ExitBattle(bot);
   var boss=RoomMgr.WorldBossRoom;
   if(bot.IsInWorldBossRoom||(boss!=null&&boss.GetPlayersSafe().Any(p=>p.PlayerId==bot.PlayerId))){BossCommand(bot,33);if(boss!=null&&boss.GetPlayersSafe().Any(p=>p.PlayerId==bot.PlayerId))boss.RemovePlayer(bot);}
   var visit=Get(bot);visit.Stage=0;visit.Allowed=false;visit.Next=DateTime.UtcNow.AddMinutes(2);visit.Last="left native Boss lobby";return true;
  }catch(Exception e){lastError=e.GetBaseException().Message;Console.WriteLine("BOT WORLD BOSS LEAVE: "+lastError);return false;}}}
  public static void Tick(IEnumerable<BotPlayer> onlineBots) {lock(Gate){try{
   var bots=onlineBots.ToArray();var boss=RoomMgr.WorldBossRoom;bool open=Open(boss);var now=DateTime.UtcNow;
   foreach(var bot in bots){var v=Get(bot);try{v.Damage=bot.AllWorldDameBoss;var room=bot.CurrentRoom;
    if(room!=null&&room.RoomType==eRoomType.WordBossFight){
     var game=room.Game as BaseGame;
     if(game!=null&&game.GameState==eGameState.ALLSessionStopped){NativeStatus(bot,3);ExitBattle(bot);v.Stage=0;v.Allowed=false;v.Next=now.AddSeconds(Math.Max(60,boss==null?60:boss.TimeCD));v.Last="native battle ended; cooldown";}
     else if(v.Stage==4&&now>=v.Next){
      if(!open){ExitBattle(bot);v.Stage=0;v.Next=now.AddMinutes(2);v.Last="activity closed before battle";continue;}
      // Use the complete sendGameRoomSetUp layout. The shipped client's older
      // enterUserGuide helper omits isOpenBoss and the final map integer.
      var enter=Packet(bot,94);enter.WriteInt(2);enter.WriteInt(boss.CurrentPVE);enter.WriteByte(14);enter.WriteBoolean(false);enter.WriteString("");enter.WriteString("世界首领挑战");enter.WriteByte((byte)(bot.PlayerCharacter.Grade<5?4:3));enter.WriteByte(0);enter.WriteInt(0);enter.WriteBoolean(false);enter.WriteInt(0);Send(bot,enter);
      v.Stage=6;v.Next=now.AddSeconds(2);v.Deadline=now.AddSeconds(15);v.Last="native map setup requested";
     }else if(v.Stage==6&&now>=v.Next){
      if(!open){ExitBattle(bot);v.Stage=0;v.Next=now.AddMinutes(2);v.Last="activity closed before battle";continue;}
      if(room.MapId!=boss.CurrentPVE){if(now<v.Deadline){v.Next=now.AddSeconds(2);continue;}ExitBattle(bot);v.Stage=0;v.Next=now.AddMinutes(2);v.Last="native map setup rejected";continue;}
      var start=Packet(bot,94);start.WriteInt(7);Send(bot,start);NativeStatus(bot,2);v.Stage=5;v.Starts++;v.Next=now.AddSeconds(20);v.Last="native battle start requested";Console.WriteLine("BOT WORLD BOSS START: "+bot.PlayerId+" pve="+boss.CurrentPVE);
     }else if(v.Stage==5&&game==null&&now>=v.Next&&!room.IsPlaying){ExitBattle(bot);v.Stage=0;v.Next=now.AddMinutes(2);v.Last="native battle admission failed; retry later";}
     continue;
    }
    if(!open){if(bot.IsInWorldBossRoom)BeforeOffline(bot);v.Last="activity closed";continue;}
    // Human invitations and existing rooms take precedence over autonomous activity.
    if(room!=null||bot.MainBag.GetItemAt(6)==null||bot.PlayerCharacter.Grade<5||now<v.Next)continue;
    if(v.Stage==0){if(now<nextAdmission)continue;nextAdmission=now.AddSeconds(3);v.Stage=1;v.Allowed=false;v.Attempts++;v.Next=now.AddSeconds(10);v.Last="native admission requested";BossCommand(bot,32);}
    else if(v.Stage==2&&v.Allowed){
     var add=Packet(bot,102);add.WriteByte(34);add.WriteInt(boss.playerDefaultPosX);add.WriteInt(boss.playerDefaultPosY);Send(bot,add);v.Stage=3;v.Next=now.AddSeconds(2);v.Last="native scene join requested";
    }else if(v.Stage==3){
     if(!bot.IsInWorldBossRoom){v.Stage=0;v.Next=now.AddMinutes(2);v.Last="scene admission rejected";continue;}
     NativeStatus(bot,1);v.Stage=4;v.Next=now.AddSeconds(1);v.Deadline=now.AddSeconds(15);var create=Packet(bot,94);create.WriteInt(0);create.WriteByte(14);create.WriteByte(3);create.WriteString("世界首领挑战");create.WriteString("");Send(bot,create);v.Last="native room requested";
    }else if(v.Stage==1||(v.Stage==4&&now>=v.Deadline)){v.Stage=0;v.Next=now.AddMinutes(2);v.Last="native admission timeout";}
   }catch(Exception e){lastError=e.GetBaseException().Message;v.Last="native error: "+lastError;v.Stage=0;v.Next=now.AddMinutes(2);try{if(bot.CurrentRoom!=null&&bot.CurrentRoom.RoomType==eRoomType.WordBossFight&&!bot.CurrentRoom.IsPlaying)ExitBattle(bot);}catch{}Console.WriteLine("BOT WORLD BOSS PLAYER "+bot.PlayerId+": "+lastError);}}
  }catch(Exception e){lastError=e.GetBaseException().Message;Console.WriteLine("BOT WORLD BOSS: "+lastError);}}}
  public static void OnPacket(BotPlayer bot,GSPacketIn original) {if(original.Code!=102)return;lock(Gate){try{
   var p=new GSPacketIn((byte[])original.Buffer.Clone(),original.Length);p.Code=original.Code;p.ClientID=original.ClientID;p.Offset=GSPacketIn.HDR_SIZE;int command=p.ReadByte();var v=Get(bot);
   if(command==2){ // CANENTER: bool allowed, bool dead, int revive cooldown, buffs.
    bool allowed=p.ReadBoolean();v.Allowed=allowed;
    if(!allowed){v.Stage=0;v.Next=DateTime.UtcNow.AddMinutes(2);v.Last="native admission rejected";return;}
    p.ReadBoolean();int cooldown=p.ReadInt();v.Stage=2;v.Next=DateTime.UtcNow.AddSeconds(Math.Max(1,cooldown));v.Last=cooldown>0?"waiting native revive cooldown":"native admission accepted";
   }else if(command==1||command==8||command==9){v.Last="native activity ended";}
  }catch(Exception e){lastError=e.GetBaseException().Message;Console.WriteLine("BOT WORLD BOSS PACKET: "+lastError);}}}
  static object LivingStatus(Living p){if(p==null)return null;return new{id=p.Id,type=p.GetType().FullName,name=p.Name,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team,living=p.IsLiving,attacking=p.IsAttacking,worldBoss=p.Config!=null&&p.Config.IsWorldBoss,x=p.X,y=p.Y};}
  static object[] FieldLivings(BaseGame game,string name){var f=typeof(BaseGame).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);var values=f==null?null:f.GetValue(game) as IEnumerable<Living>;return values==null?new object[0]:values.Select(LivingStatus).ToArray();}
  static object GameStatus(int id){try{var bot=WorldMgr.GetPlayerById(id);var room=bot==null?null:bot.CurrentRoom;if(room==null||room.RoomType!=eRoomType.WordBossFight)return null;var game=room.Game as BaseGame;if(game==null)return new{id,room=room.RoomId,game=room.Game==null?"pending":room.Game.GetType().FullName};var pve=game as PVEGame;
   return new{id,room=room.RoomId,game=game.GetType().FullName,state=game.ToString(),turn=game.TurnIndex,currentIsNull=game.CurrentLiving==null,currentType=game.CurrentLiving==null?null:game.CurrentLiving.GetType().FullName,current=game.CurrentLiving==null?null:game.CurrentLiving.Name,currentPveIsNull=pve==null||pve.CurrentTurnLiving==null,currentPveType=pve==null||pve.CurrentTurnLiving==null?null:pve.CurrentTurnLiving.GetType().FullName,currentPve=pve==null||pve.CurrentTurnLiving==null?null:pve.CurrentTurnLiving.Name,currentDetails=LivingStatus(game.CurrentLiving),lived=(game.GetLivedLivings()??new List<Living>()).Select(LivingStatus).ToArray(),rawLivings=FieldLivings(game,"m_livings"),rawDeck=FieldLivings(game,"m_decklivings"),actors=game.GetAllPlayers().Select(p=>new{id=p.PlayerDetail.PlayerCharacter.ID,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team,living=p.IsLiving,attacking=p.IsAttacking,x=p.X,y=p.Y}).ToArray(),bosses=game.FindAllBoss().Select(p=>new{id=p.Id,name=p.Name,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team,living=p.IsLiving,x=p.X,y=p.Y}).ToArray(),simpleNpcs=game.FindAllNpcLiving().Length};
  }catch(Exception e){return new{id,error=e.GetBaseException().Message};}}
  public static object Status() {lock(Gate){try{var b=RoomMgr.WorldBossRoom;return new{available=b!=null,open=Open(b),name=b==null?"":b.Name,pve=b==null?0:b.CurrentPVE,blood=b==null?0:b.Blood,maxBlood=b==null?0:b.MaxBlood,begin=b==null?DateTime.MinValue:b.BeginTime,end=b==null?DateTime.MinValue:b.EndTime,roomClosed=b==null||b.RoomClose,fightOver=b==null||b.FightOver,ticketId=b==null?0:b.TicketID,ticketCount=b==null?0:b.TicketCount,cooldownSeconds=b==null?0:b.TimeCD,participants=b==null?new int[0]:b.GetPlayersSafe().Select(p=>p.PlayerId).ToArray(),visits=Visits.Select(x=>new{id=x.Key,stage=x.Value.Stage,attempts=x.Value.Attempts,starts=x.Value.Starts,damage=x.Value.Damage,next=x.Value.Next,note=x.Value.Last,nativeCommand=x.Value.NativeCommand}).ToArray(),games=Visits.Keys.Select(GameStatus).Where(x=>x!=null).ToArray(),error=lastError};}catch(Exception e){return new{available=false,error=e.GetBaseException().Message};}}}
 }
}
