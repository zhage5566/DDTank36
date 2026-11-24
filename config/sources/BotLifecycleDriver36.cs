using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Game.Server.GameObjects;
using Game.Server.Managers;
using Game.Server.Rooms;
using Game.Base.Packets;
using Game.Logic;
namespace Local36 {
 public static class BotLifecycleDriver36 {
  sealed class Run {public int Busy,Version,ActionsLeft,Stage=-1,Stalls;public DateTime Next,RoomSince,LastInvite,PlanUntil;public string[] Priorities;public string ConfigKey,LastResult;public BaseRoom Room;public bool WorldBoss;public readonly Dictionary<int,DateTime> Invited=new Dictionary<int,DateTime>();}
  sealed class Choice {public string action;public int questId,itemId,rewardItemId,mapId,hardLevel=-1,parameter,resourceItemId;}
  static readonly ConcurrentDictionary<int,Run> Runs=new ConcurrentDictionary<int,Run>();
  static volatile Dictionary<int,LifecycleConfig36> Configs=new Dictionary<int,LifecycleConfig36>();static DateTime ReloadAt;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  public static void Refresh(bool force){if(force||DateTime.UtcNow>=ReloadAt){ReloadAt=DateTime.UtcNow.AddSeconds(5);Configs=LifecycleCatalog36.Enabled().ToDictionary(c=>c.userId);}}
  public static void Invalidate(int id){Run run;if(Runs.TryGetValue(id,out run))lock(run){Interlocked.Increment(ref run.Version);run.Priorities=null;run.ConfigKey=null;run.ActionsLeft=0;run.PlanUntil=DateTime.MinValue;run.Next=DateTime.UtcNow;run.WorldBoss=false;}LifecycleCatalog36.ClearPlan(id);}
  public static bool Enabled(int id){return Configs.ContainsKey(id);}
  public static bool AllowsWorldBoss(int id){Run run;return !Enabled(id)||(Runs.TryGetValue(id,out run)&&run.WorldBoss);}
  public static bool OwnsRoom(GamePlayer player){Run run;return player.CurrentRoom!=null&&Runs.TryGetValue(player.PlayerId,out run)&&Object.ReferenceEquals(run.Room,player.CurrentRoom);}
  static string Fingerprint(LifecycleConfig36 c,BotLlmConfig m,int stage){string raw=c.autoQuest+"/"+c.autoStrengthen+"/"+c.invitePlayers+"/"+c.maxStrengthen+"/"+c.invitationInterval+"/"+stage+"/"+m.BaseUrl+"/"+m.Model+"/"+m.SystemPrompt;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","");}
  static void Persist(int id,Run r){LifecycleCatalog36.SavePlan(id,new LifecyclePlan36{configKey=r.ConfigKey,priorities=r.Priorities,actionsLeft=r.ActionsLeft,untilUtc=r.PlanUntil});}
  static bool GrowthAction(LifecycleConfig36 c,string a){return a=="quest_add"||a=="quest_finish"?c.autoQuest:c.autoStrengthen;}
  public static void Maintenance(BotPlayer b){if(b.CurrentRoom!=null||!Lifecycle36.ForgeBusy(b))return;Run r;bool active=Enabled(b.PlayerId)&&Runs.TryGetValue(b.PlayerId,out r)&&r.Priorities!=null&&r.ActionsLeft>0&&DateTime.UtcNow<r.PlanUntil;var model=BotCatalog.ReadLlm(b.PlayerId);if(active&&model!=null&&model.Enabled&&!String.IsNullOrWhiteSpace(model.ApiKey))return;lock(b){var step=Lifecycle36.CancelForge(b,b.BotClient.Receive);if(step.acted){b.SaveIntoDatabase();LifecycleCatalog36.SetState(b.PlayerId,"成长暂停，已通过原生背包归还暂存物品");}}}
  public static bool CanUseGrowthItems(GamePlayer player){if(!Enabled(player.PlayerId))return true;Run run;if(!Runs.TryGetValue(player.PlayerId,out run)||run.Priorities==null||run.ActionsLeft<=0||DateTime.UtcNow>=run.PlanUntil)return false;var model=BotCatalog.ReadLlm(player.PlayerId);return model!=null&&model.Enabled&&!String.IsNullOrWhiteSpace(model.ApiKey);}
  public static void Tick(IEnumerable<BotPlayer> players,Func<int,bool> eligible){
   Refresh(false);
   var online=players.ToDictionary(p=>p.PlayerId);
   foreach(var config in Configs.Values){BotPlayer bot;if(!online.TryGetValue(config.userId,out bot)){LifecycleCatalog36.SetState(config.userId,"当前批次离线，保留进度，等待下次上线后由LLM继续");continue;}
    var run=Runs.GetOrAdd(bot.PlayerId,id=>new Run());if(!eligible(bot.PlayerId)||DateTime.UtcNow<run.Next||Interlocked.CompareExchange(ref run.Busy,1,0)!=0)continue;
    run.Next=DateTime.UtcNow.AddSeconds(30);
    try{
     var model=BotCatalog.ReadLlm(bot.PlayerId);if(model==null||!model.Enabled||String.IsNullOrWhiteSpace(model.ApiKey)){LifecycleCatalog36.SetState(bot.PlayerId,"LLM未启用或缺少密钥，暂停成长动作");Interlocked.Exchange(ref run.Busy,0);continue;}
     if(bot.CurrentSpaRoom!=null||bot.CurrentMarryRoom!=null||bot.IsInWorldBossRoom||(bot.CurrentRoom!=null&&(bot.CurrentRoom.IsPlaying||bot.CurrentRoom.Host!=bot))){LifecycleCatalog36.SetState(bot.PlayerId,"当前参与原生场景或战斗，等待结束再由LLM规划成长");Interlocked.Exchange(ref run.Busy,0);continue;}
     LifecycleInspection36 view;lock(bot)view=Lifecycle36.Inspect(bot);var choices=Choices(bot,run,config,view);
     int stage=bot.PlayerCharacter.Grade/10;string configKey=Fingerprint(config,model,stage);
     if(run.ConfigKey!=configKey||run.Stage!=stage){run.Priorities=null;run.ConfigKey=configKey;run.Stage=stage;var saved=LifecycleCatalog36.ReadPlan(bot.PlayerId);if(saved!=null&&saved.configKey==configKey&&saved.priorities!=null&&saved.actionsLeft>0&&saved.untilUtc>DateTime.UtcNow){run.Priorities=saved.priorities;run.ActionsLeft=saved.actionsLeft;run.PlanUntil=DateTime.SpecifyKind(saved.untilUtc,DateTimeKind.Utc);}}
     if(run.Priorities!=null&&run.ActionsLeft>0&&DateTime.UtcNow<run.PlanUntil){
      var next=run.Priorities.SelectMany(name=>choices.Where(c=>c.action==name&&name!="wait")).FirstOrDefault();
      if(next==null){LifecycleCatalog36.SetState(bot.PlayerId,"缓存LLM策略等待真实任务或资源变化，未额外调用模型");run.Next=DateTime.UtcNow.AddSeconds(60);Interlocked.Exchange(ref run.Busy,0);continue;}
      run.ActionsLeft--;Persist(bot.PlayerId,run);run.Next=DateTime.UtcNow.AddSeconds(15);int version=Thread.VolatileRead(ref run.Version);ThreadPool.QueueUserWorkItem(_=>Apply(bot,run,Json.Serialize(next),eligible,version));continue;
     }
     var humans=AvailableHumans(bot).Take(10).Select(p=>new{id=p.PlayerId,name=p.PlayerCharacter.NickName,level=p.PlayerCharacter.Grade,friend=bot.Friends!=null&&bot.Friends.ContainsKey(p.PlayerId)}).ToArray();
     var snapshot=new{level=view.level,gp=view.gp,gold=view.gold,room=bot.CurrentRoom==null?0:bot.CurrentRoom.RoomId,quests=view.quests.Take(3).Select(q=>new{id=q.id,name=q.name,done=q.canFinish,dungeon=q.needsDungeon,map=q.mapId}),forge=view.forgeBusy};
     string summary=Json.Serialize(snapshot);if(summary.Length>700)summary=summary.Substring(0,700);
     string[] policyActions=Lifecycle36.SupportedActions().Where(a=>GrowthAction(config,a)).Concat(config.autoQuest?new[]{"prepare_dungeon","start_dungeon","world_boss"}:new string[0]).Concat(config.invitePlayers?new[]{"invite_player"}:new string[0]).Concat(new[]{"wait"}).Distinct().ToArray();
     string prompt="allowedActions="+Json.Serialize(policyActions)+"。为节省Token，只制定动作优先级策略，不逐步报ID；输出{\"priorities\":[\"动作名\"],\"maxActions\":256,\"ttlMinutes\":240,\"reason\":\"短原因\"}。本地每一步按真实许可清单选ID并校验，不会越过材料/任务条件。副本先邀请玩家，避免无目的接满支线。在线玩家数="+humans.Length+"，真实摘要="+summary;
     LifecycleCatalog36.SetState(bot.PlayerId,"正在请求LLM，选择下一项真实成长动作");
     int decisionVersion=Thread.VolatileRead(ref run.Version);bool submitted=Llm36.Submit(bot.PlayerId,"lifecycle_plan",prompt,"lifecycle/self",text=>ThreadPool.QueueUserWorkItem(_=>AcceptPolicy(bot,run,text,policyActions,eligible,decisionVersion)),()=>{run.Priorities=null;run.Next=DateTime.UtcNow.AddMinutes(5);LifecycleCatalog36.SetState(bot.PlayerId,"模型调用失败，暂停成长并在5分钟后重试，避免重复耗Token");Interlocked.Exchange(ref run.Busy,0);},true);
     if(!submitted){LifecycleCatalog36.SetState(bot.PlayerId,"LLM当前不可用，未执行成长动作");Interlocked.Exchange(ref run.Busy,0);}
    }catch(Exception e){LifecycleCatalog36.SetState(bot.PlayerId,"成长状态读取失败："+e.GetBaseException().Message);Interlocked.Exchange(ref run.Busy,0);}
   }
  }
  static Choice[] Choices(BotPlayer bot,Run run,LifecycleConfig36 config,LifecycleInspection36 view){
   var list=new List<Choice>{new Choice{action="wait"}};
   if(bot.CurrentRoom==null){
    foreach(var a in view.actions.Where(a=>GrowthAction(config,a.action)&&(a.action!="strengthen"||view.forgeBusy||bot.MainBag.GetItems().Any(i=>i!=null&&i.ItemID==a.itemId&&i.StrengthenLevel<config.maxStrengthen))))list.Add(new Choice{action=a.action,questId=a.questId,itemId=a.itemId,rewardItemId=a.rewardItemId,parameter=a.parameter,resourceItemId=a.resourceItemId});
    if(config.autoQuest&&!view.forgeBusy)foreach(var q in view.quests.Where(q=>(q.needsDungeon||q.needsWorldBoss)&&q.mapId>0&&q.hardLevel>=0))list.Add(new Choice{action=q.needsWorldBoss?"world_boss":"prepare_dungeon",questId=q.id,mapId=q.mapId,hardLevel=q.hardLevel});
   }else if(bot.CurrentRoom.Host==bot&&!bot.CurrentRoom.IsPlaying&&bot.CurrentRoom.RoomType==eRoomType.Dungeon){
    if(config.invitePlayers&&DateTime.UtcNow>=run.LastInvite.AddSeconds(config.invitationInterval)&&AvailableHumans(bot).Any())list.Add(new Choice{action="invite_player",mapId=bot.CurrentRoom.MapId});
    if((DateTime.UtcNow-run.RoomSince).TotalSeconds>=45&&bot.CurrentRoom.CanStart())list.Add(new Choice{action="start_dungeon",mapId=bot.CurrentRoom.MapId});
   }
   return list.ToArray();
  }
  static void AcceptPolicy(BotPlayer bot,Run run,string text,string[] allowed,Func<int,bool> eligible,int version){try{lock(run){
   if(version!=Thread.VolatileRead(ref run.Version))return;
   if(!eligible(bot.PlayerId)||!LifecycleCatalog36.Read(bot.PlayerId).enabled)return;
   var plan=Json.Deserialize<Dictionary<string,object>>(text);object values;var sequence=plan.TryGetValue("priorities",out values)?values as System.Collections.IEnumerable:null;if(sequence==null)throw new ArgumentException("模型未返回优先级策略");
   var priorities=new List<string>();foreach(var value in sequence){string name=Convert.ToString(value);if(!allowed.Contains(name))throw new ArgumentException("策略包含未开放动作");if(!priorities.Contains(name))priorities.Add(name);}
   if(priorities.Count==0)throw new ArgumentException("策略为空");run.Priorities=priorities.ToArray();run.ActionsLeft=Math.Max(32,Math.Min(256,Int(plan,"maxActions")<=0?256:Int(plan,"maxActions")));int minutes=Math.Max(10,Math.Min(240,Int(plan,"ttlMinutes")<=0?240:Int(plan,"ttlMinutes")));run.PlanUntil=DateTime.UtcNow.AddMinutes(minutes);run.Next=DateTime.UtcNow;run.Stalls=0;Persist(bot.PlayerId,run);
   LifecycleCatalog36.SetState(bot.PlayerId,"已缓存LLM策略："+String.Join("→",run.Priorities)+"；最多"+run.ActionsLeft+"次原生动作，"+minutes+"分钟内复用，期间不逐步调用模型");
  }}catch(Exception e){run.Priorities=null;run.Next=DateTime.UtcNow.AddMinutes(5);LifecycleCatalog36.SetState(bot.PlayerId,"规划未采用："+e.Message+"；等待重试");}finally{Interlocked.Exchange(ref run.Busy,0);}}
  static void Apply(BotPlayer bot,Run run,string text,Func<int,bool> eligible,int version){try{lock(bot){
   if(version!=Thread.VolatileRead(ref run.Version))return;
   var config=LifecycleCatalog36.Read(bot.PlayerId);if(!config.enabled||!eligible(bot.PlayerId)||!bot.Online){LifecycleCatalog36.SetState(bot.PlayerId,"模式暂停或角色下线，旧决策未执行");return;}
   if(bot.CurrentSpaRoom!=null||bot.CurrentMarryRoom!=null||bot.IsInWorldBossRoom||BotService.HasPendingInvitation(bot.PlayerId)||(bot.CurrentRoom!=null&&(bot.CurrentRoom.IsPlaying||bot.CurrentRoom.Host!=bot))){LifecycleCatalog36.SetState(bot.PlayerId,"场景或玩家邀请已变化，延后缓存成长动作");return;}
   var currentModel=BotCatalog.ReadLlm(bot.PlayerId);if(currentModel==null||!currentModel.Enabled||String.IsNullOrWhiteSpace(currentModel.ApiKey)||run.ConfigKey!=Fingerprint(config,currentModel,bot.PlayerCharacter.Grade/10)){LifecycleCatalog36.SetState(bot.PlayerId,"模型或成长策略已变化，旧动作未执行");return;}
   var data=Json.Deserialize<Dictionary<string,object>>(text);if(data==null)throw new ArgumentException("模型未返回决策JSON");string action=Text(data,"action");int quest=Int(data,"questId"),item=Int(data,"itemId"),reward=Int(data,"rewardItemId"),map=Int(data,"mapId");
   var view=Lifecycle36.Inspect(bot);var choices=Choices(bot,run,config,view);var selected=choices.FirstOrDefault(a=>a.action==action&&a.questId==quest&&a.itemId==item&&a.rewardItemId==reward&&a.mapId==map&&a.hardLevel==Int(data,"hardLevel")&&a.parameter==Int(data,"parameter")&&a.resourceItemId==Int(data,"resourceItemId"));
   if(selected==null)throw new ArgumentException("模型选择不在当前许可清单中，未执行");
   if(action=="wait"){LifecycleCatalog36.SetState(bot.PlayerId,"LLM选择等待："+Text(data,"reason"));run.Next=DateTime.UtcNow.AddSeconds(60);return;}
   if(view.actions.Any(a=>a.action==action)){
    var result=Lifecycle36.Execute(bot,new LifecycleAction36{action=action,questId=quest,itemId=item,rewardItemId=reward,parameter=selected.parameter,resourceItemId=selected.resourceItemId},config.maxStrengthen,bot.BotClient.Receive);
    if(result.acted){BotAppearance36.Refresh(bot,false);NativeStats36.Recalculate(bot);}bot.SaveIntoDatabase();if(result.acted)RankSync36.MarkDirty();LifecycleCatalog36.SetState(bot.PlayerId,result.state+"；缓存策略剩余"+run.ActionsLeft+"步");if(!result.acted){if(run.LastResult==result.state)run.Stalls++;else run.Stalls=1;if(run.Stalls>=3){run.Priorities=null;LifecycleCatalog36.ClearPlan(bot.PlayerId);run.Next=DateTime.UtcNow.AddMinutes(5);}}else run.Stalls=0;run.LastResult=result.state;Console.WriteLine("LIFECYCLE ACTION: "+bot.PlayerId+" "+action+" acted="+result.acted);return;
   }
   if(action=="world_boss"){run.WorldBoss=true;LifecycleCatalog36.SetState(bot.PlayerId,"LLM选择世界Boss任务，等待原生活动入场及条件");return;}
   if(action=="prepare_dungeon"){
    if(bot.CurrentRoom!=null||Lifecycle36.ForgeBusy(bot))throw new InvalidOperationException("当前不具备建房条件");
    var task=view.quests.FirstOrDefault(q=>q.id==quest&&q.needsDungeon&&q.mapId==map&&q.hardLevel==selected.hardLevel);if(task==null)throw new InvalidOperationException("副本任务状态已变化");
    RoomMgr.CreateRoom(bot,"任务副本 · "+task.name,"",eRoomType.Dungeon,20);
    for(int i=0;i<20&&bot.CurrentRoom==null;i++)Thread.Sleep(150);var room=bot.CurrentRoom;if(room==null)throw new InvalidOperationException("原生副本建房未完成");
    room.MapId=map;room.HardLevel=(eHardLevel)task.hardLevel;room.LevelLimits=0;room.UpdateRoomGameType();BotService.OpenBotSeats(bot,4);RoomMgr.UpdatePlayerState(bot,1);run.Room=room;run.RoomSince=DateTime.UtcNow;run.WorldBoss=false;run.LastInvite=DateTime.MinValue;
    LifecycleCatalog36.SetState(bot.PlayerId,"LLM已选择任务副本并建房，等待模型邀请玩家及准备");run.Next=DateTime.UtcNow.AddSeconds(1);return;
   }
   if(action=="invite_player"){
    var candidates=AvailableHumans(bot).Where(p=>!run.Invited.ContainsKey(p.PlayerId)||(DateTime.UtcNow-run.Invited[p.PlayerId]).TotalSeconds>=config.invitationInterval).ToArray();int target=Int(data,"targetPlayerId");var human=candidates.FirstOrDefault(p=>p.PlayerId==target)??candidates.OrderByDescending(p=>bot.Friends!=null&&bot.Friends.ContainsKey(p.PlayerId)).ThenBy(p=>Math.Abs(p.PlayerCharacter.Grade-bot.PlayerCharacter.Grade)).FirstOrDefault();
    if(human==null){LifecycleCatalog36.SetState(bot.PlayerId,"没有当前可邀请的在线玩家，继续等待");return;}
    var request=new GSPacketIn(70,bot.PlayerId);request.WriteInt(human.PlayerId);request.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(request);run.Invited[human.PlayerId]=DateTime.UtcNow;run.LastInvite=DateTime.UtcNow;
    BotService.NotifySocial(bot,human,"dungeon_invitation_sent","角色有副本任务，已经向玩家发出原生房间邀请，等待玩家选择是否接受。","我在做副本任务，想邀请你一起打，方便的话进房一起吧。");LifecycleCatalog36.SetState(bot.PlayerId,"已私信并邀请"+human.PlayerCharacter.NickName+"，等待玩家同意和准备");return;
   }
   if(action=="start_dungeon"){var room=bot.CurrentRoom;if(room==null||room.Host!=bot||room.IsPlaying||!room.CanStart())throw new InvalidOperationException("原生房间未达到开始条件");RoomMgr.StartGame(room);LifecycleCatalog36.SetState(bot.PlayerId,"LLM选择开始任务副本，已提交原生开始请求");return;}
  }}catch(Exception e){LifecycleCatalog36.SetState(bot.PlayerId,"成长决策未执行："+e.GetBaseException().Message);}finally{Interlocked.Exchange(ref run.Busy,0);}}
  static GamePlayer[] AvailableHumans(BotPlayer bot){return WorldMgr.GetAllPlayers().Where(p=>!BotService.IsBot(p.PlayerId)&&!p.IsRobot&&p.PlayerId!=bot.PlayerId&&p.IsActive&&p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&!p.IsInWorldBossRoom&&p.AreaID==bot.AreaID).ToArray();}
  static string Text(Dictionary<string,object> values,string key){object value;return values.TryGetValue(key,out value)?Convert.ToString(value)??"":"";}
  static int Int(Dictionary<string,object> values,string key){int value;return Int32.TryParse(Text(values,key),out value)?value:0;}
 }
}
