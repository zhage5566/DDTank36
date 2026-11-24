using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Web.Script.Serialization;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Quests;
using Bussiness.Managers;
using SqlDataProvider.Data;
using Game.Logic;
using Game.Server.Managers;

namespace Local36 {
 public class LifecycleStep36 {
  public int questId,mapId,missionId,roomType,itemId,hardLevel=-1;
  public string questName,state,action;
  public bool needsDungeon,needsWorldBoss,forgeBusy,acted;
 }
 public class LifecycleAction36 {
  public string action,description;public int questId,itemId,rewardItemId,parameter,resourceItemId;
 }
 public class LifecycleCondition36 {
  public int id,type,para1,para2,value;public string title,nativeType;public bool optional,completed,nativeCompleted;
 }
 public class LifecycleQuest36 {
  public int id,mapId,missionId,roomType,hardLevel=-1;public string name,detail,objective,state,mappingEvidence;
  public bool accepted,completed,canFinish,needsDungeon,needsWorldBoss;
  public int rewardGP,rewardGold;public object[] rewards;public LifecycleCondition36[] conditions;
 }
 public class LifecycleInspection36 {
  public int userId,level,gp,gold;public string state;public bool forgeBusy;
  public LifecycleQuest36[] quests,availableQuests;public LifecycleAction36[] actions;public object[] capabilities;public object forge,resources,nativeSystems;
 }
 // Original 3.6 inventories own every completion, reward, item and currency change.
 // Inspection never requests progress packets (181) or rewrites condition values.
 public static class Lifecycle36 {
  public static string[] SupportedActions(){return new[]{"quest_add","quest_finish","strengthen","texp","pet_adopt","pet_equip","pet_feed","card_equip","card_upgrade","compose","inlay","totem_activate","necklace_cast"};}
  static readonly object Gate=new object();
  static T Read<T>(object obj,string name,T fallback){if(obj==null)return fallback;try{var t=obj.GetType();var p=t.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(p!=null)return (T)p.GetValue(obj,null);var f=t.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);return f==null?fallback:(T)f.GetValue(obj);}catch{return fallback;}}
  static List<BaseQuest> Active(GamePlayer p){var list=Read<List<BaseQuest>>(p.QuestInventory,"m_list",null);if(list==null)return new List<BaseQuest>();lock(list)return list.ToList();}
  static IEnumerable<QuestInfo> Catalog(){var field=typeof(QuestMgr).GetFields(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).FirstOrDefault(f=>f.FieldType==typeof(Dictionary<int,QuestInfo>));var dict=field==null?null:field.GetValue(null) as Dictionary<int,QuestInfo>;return dict==null?new QuestInfo[0]:dict.Values.ToArray();}
  static bool Done(GamePlayer p,int id){if(id<1)return true;var active=p.QuestInventory.FindQuest(id);if(active!=null&&active.Data.IsComplete)return true;var bitmap=Read<byte[]>(p.PlayerCharacter,"QuestSite",null);if(bitmap==null){var f=p.QuestInventory.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance).FirstOrDefault(x=>x.FieldType==typeof(byte[]));if(f!=null)bitmap=f.GetValue(p.QuestInventory) as byte[];}int n=id-1;return bitmap!=null&&n/8<bitmap.Length&&(bitmap[n/8]&(1<<(n%8)))!=0;}
  static bool Candidate(GamePlayer p,QuestInfo q){
   int level=p.PlayerCharacter.Grade;if(q==null||q.CanRepeat||level<q.NeedMinLevel||(q.NeedMaxLevel>0&&level>q.NeedMaxLevel)||p.QuestInventory.FindQuest(q.ID)!=null||Done(p,q.ID))return false;
   if(q.TimeMode&&(DateTime.Now<q.StartDate||DateTime.Now>q.EndDate))return false;
   if(q.IsOther==1&&p.PlayerCharacter.ConsortiaID==0)return false;if(q.IsOther==2&&!p.PlayerCharacter.IsMarried)return false;
   foreach(string part in (q.PreQuestID??"").Split(',')){int id;if(Int32.TryParse(part,out id)&&id>0&&!Done(p,id))return false;}return true;
  }
  static bool ActualStrengthen(GamePlayer p,QuestConditionInfo condition){return Enumerable.Range(0,31).Select(slot=>p.MainBag.GetItemAt(slot)).Any(i=>i!=null&&i.IsExist&&i.Count==1&&i.Template!=null&&i.Template.CategoryID==condition.Para1&&i.StrengthenLevel>=condition.Para2);}
  static bool ActualClaimGate(GamePlayer p,QuestInfo info){return (QuestMgr.GetQuestCondiction(info)??new List<QuestConditionInfo>()).Where(c=>!c.isOpitional&&c.CondictionType==9).All(c=>ActualStrengthen(p,c));}
  static LifecycleQuest36 Describe(GamePlayer p,QuestInfo info,BaseQuest active){
   var conditions=QuestMgr.GetQuestCondiction(info)??new List<QuestConditionInfo>();var result=new LifecycleQuest36{id=info.ID,name=info.Title,detail=info.Detail,objective=info.Objective,accepted=active!=null,completed=Done(p,info.ID),rewardGP=info.RewardGP,rewardGold=info.RewardGold};
   result.conditions=conditions.Select(c=>{var n=active==null?null:active.GetConditionById(c.CondictionID);bool complete=false;try{complete=n!=null&&n.IsCompleted(p);}catch{}return new LifecycleCondition36{id=c.CondictionID,type=c.CondictionType,para1=c.Para1,para2=c.Para2,title=c.CondictionTitle,optional=c.isOpitional,value=n==null?0:n.Value,nativeType=n==null?"not_accepted":n.GetType().FullName,nativeCompleted=complete,completed=complete&&(c.CondictionType!=9||ActualStrengthen(p,c))};}).ToArray();
   try{result.canFinish=active!=null&&!active.Data.IsComplete&&active.CanCompleted(p)&&ActualClaimGate(p,info);}catch{result.state="原生完成检查失败";}
   var awards=QuestMgr.GetQuestGoods(info)??new List<QuestAwardInfo>();result.rewards=awards.Select(a=>(object)new{itemId=a.RewardItemID,count=a.RewardItemCount,selected=a.IsSelect,validDays=a.RewardItemValid,strengthen=a.StrengthenLevel}).ToArray();
   // Same-version condition 21 is GameMissionOver. Mission identifiers are not NPC templates.
   var mission=result.conditions.FirstOrDefault(c=>c.type==21&&!c.completed);if(mission!=null)result.missionId=mission.para1;
   PveInfo map=null;try{if(info.MapID>0)map=PveInfoMgr.GetPveInfoById(info.MapID);}catch{}
   bool nativeDungeon=result.conditions.Any(c=>!c.completed&&(c.type==21||c.nativeType.IndexOf("GameCopy",StringComparison.OrdinalIgnoreCase)>=0||c.nativeType.IndexOf("GameMission",StringComparison.OrdinalIgnoreCase)>=0));
   string nativeTitles=String.Join("；",result.conditions.Where(c=>!c.completed).Select(c=>c.title??""));
   if(map==null&&nativeDungeon){try{var matches=PveInfoMgr.GetPveInfo().Where(m=>!String.IsNullOrWhiteSpace(m.Name)&&nativeTitles.IndexOf(m.Name,StringComparison.Ordinal)>=0).ToArray();if(matches.Length==1){map=matches[0];result.mappingEvidence="原生Quest_Condiction.CondictionTitle精确包含唯一Pve.Name："+nativeTitles;}}catch{}}
   if(nativeTitles.IndexOf("英雄",StringComparison.Ordinal)>=0)result.hardLevel=3;else if(nativeTitles.IndexOf("困难",StringComparison.Ordinal)>=0)result.hardLevel=2;else if(nativeTitles.IndexOf("普通",StringComparison.Ordinal)>=0)result.hardLevel=1;else if(nativeTitles.IndexOf("简单",StringComparison.Ordinal)>=0)result.hardLevel=0;
   if(map!=null&&nativeDungeon){result.mapId=map.ID;result.roomType=map.Type;result.needsWorldBoss=map.Type==14;result.needsDungeon=!result.needsWorldBoss;if(result.mappingEvidence==null)result.mappingEvidence="原生Quest.MapID→Pve.ID";if(map.Type==10||map.Type==14)result.hardLevel=0;}
   else if(nativeDungeon)result.state="等待副本任务：原生任务未提供可确认的副本ID，保留任务条件";
   if(result.state==null)result.state=result.canFinish?"可领取原生奖励":active==null?"符合已核验的接取前提，仍由原生AddQuest最终校验":"等待真实任务条件";return result;
  }
  public static LifecycleInspection36 Inspect(GamePlayer p){if(p==null)throw new ArgumentNullException("p");lock(Gate){
   var active=Active(p);var quests=active.Where(q=>q.Data.IsExist).Select(q=>Describe(p,q.Info,q)).ToArray();var candidates=Catalog().Where(q=>Candidate(p,q)).OrderBy(q=>q.NeedMinLevel).ThenBy(q=>q.ID).Take(48).Select(q=>Describe(p,q,null)).ToArray();var actions=new List<LifecycleAction36>();
   foreach(var q in quests.Where(q=>q.canFinish)){var awards=QuestMgr.GetQuestGoods(QuestMgr.GetSingleQuest(q.id))??new List<QuestAwardInfo>();var optional=awards.Where(a=>a.IsSelect).ToArray();if(optional.Length==0)actions.Add(new LifecycleAction36{action="quest_finish",questId=q.id,description="领取已满足原生条件任务："+q.name});else foreach(var reward in optional)actions.Add(new LifecycleAction36{action="quest_finish",questId=q.id,rewardItemId=reward.RewardItemID,description="领取原生可选奖励："+q.name});}
   foreach(var q in candidates)actions.Add(new LifecycleAction36{action="quest_add",questId=q.id,description="接取原生任务："+q.name});
   var view=new LifecycleInspection36{userId=p.PlayerId,level=p.PlayerCharacter.Grade,gp=p.PlayerCharacter.GP,gold=p.PlayerCharacter.Gold,quests=quests,availableQuests=candidates,actions=actions.ToArray(),state="只读真实状态；由LLM选择许可动作",capabilities=Capabilities()};InspectForge(p,view,actions);InspectTexp(p,view,actions);InspectPetCard(p,view,actions);InspectCraft(p,view,actions);NativeGrowth36.Inspect(p,view,actions);view.actions=actions.ToArray();return view;
  }}
  public static LifecycleStep36 Step(GamePlayer p,bool autoQuest,bool autoStrengthen,int maxStrengthen,Action<GSPacketIn> receive,int preferredQuestId=0,int preferredItemId=0,int preferredRewardId=0){
   if(p==null)throw new ArgumentNullException("p");lock(Gate){var view=Inspect(p);if(!autoQuest&&!autoStrengthen)return Need(view,"等待LLM许可；未执行任何修改");if(p.CurrentRoom!=null)return Need(view,"当前在房间中，等待结束再执行成长动作");
   if(autoStrengthen)return Forge(p,maxStrengthen,receive,preferredItemId,view);
   if(view.forgeBusy)return Need(view,"铁匠铺暂存中，等待LLM选择strengthen归还装备");
   var choice=view.actions.FirstOrDefault(a=>a.action=="quest_finish"&&(preferredQuestId==0||a.questId==preferredQuestId)&&(preferredRewardId==0||a.rewardItemId==preferredRewardId));
   if(choice!=null){var q=p.QuestInventory.FindQuest(choice.questId);if(q==null||!q.CanCompleted(p)||!ActualClaimGate(p,q.Info))return Need(view,"原生任务条件/实际装备强化未满足，等待真实进度");int gp=p.PlayerCharacter.GP,level=p.PlayerCharacter.Grade;bool ok=p.QuestInventory.Finish(q,choice.rewardItemId);if(ok){p.UpdateProperties();p.UpdateFightPower();}return new LifecycleStep36{questId=choice.questId,questName=q.Info.Title,action="quest_finish",acted=ok,state=ok?"原生领奖完成；GP "+gp+"→"+p.PlayerCharacter.GP+"，等级 "+level+"→"+p.PlayerCharacter.Grade:"原生领奖拒绝（背包/条件/奖励检查）"};}
   choice=view.actions.FirstOrDefault(a=>a.action=="quest_add"&&(preferredQuestId==0||a.questId==preferredQuestId));if(choice!=null){string msg="";var q=QuestMgr.GetSingleQuest(choice.questId);bool ok=q!=null&&p.QuestInventory.AddQuest(q,out msg);return new LifecycleStep36{questId=choice.questId,questName=q==null?"":q.Title,action="quest_add",acted=ok,state=ok?"已通过原生AddQuest接取任务":"原生接任务拒绝："+msg};}
   return Need(view,preferredQuestId>0?"所选任务目前没有接取/领奖许可动作":"没有可接取或可领奖任务，等待真实条件");
  }}
  public static LifecycleStep36 Execute(GamePlayer p,LifecycleAction36 selection,int maxStrengthen,Action<GSPacketIn> receive){if(p!=null&&selection!=null&&selection.action=="pet_adopt"&&selection.parameter==-1){return NativePetAdopt36.Adopt(p,selection.itemId);}if(p==null)throw new ArgumentNullException("p");if(selection==null)throw new ArgumentNullException("selection");lock(Gate){var view=Inspect(p);if(p.CurrentRoom!=null)return Need(view,"当前在房间中，等待结束后执行成长动作");var allowed=view.actions.FirstOrDefault(a=>a.action==selection.action&&a.itemId==selection.itemId&&a.questId==selection.questId&&a.rewardItemId==selection.rewardItemId&&a.parameter==selection.parameter&&a.resourceItemId==selection.resourceItemId);if(allowed==null)return Need(view,"所选动作/精确参数不在本次原生状态许可清单中");if(allowed.action=="quest_add"||allowed.action=="quest_finish")return Step(p,true,false,maxStrengthen,receive,allowed.questId,0,allowed.rewardItemId);if(allowed.action=="strengthen")return Forge(p,maxStrengthen,receive,allowed.itemId,view);if(allowed.action=="texp")return ExecuteTexp(p,allowed,receive,view);if(allowed.action=="pet_refresh"||allowed.action=="pet_adopt")return ExecutePetAdopt(p,allowed,receive,view);if(allowed.action=="pet_feed")return ExecutePetFeed(p,allowed,receive,view);if(allowed.action=="pet_equip"||allowed.action=="card_equip"||allowed.action=="card_upgrade")return ExecutePetCard(p,allowed,receive,view);if(allowed.action=="compose"||allowed.action=="inlay")return ExecuteCraft(p,allowed,receive,view);if(allowed.action=="totem_activate"||allowed.action=="necklace_cast")return NativeGrowth36.Execute(p,allowed,receive);return Need(view,"该原生系统执行器尚未开放");}}
  static LifecycleStep36 Need(LifecycleInspection36 v,string state){var q=v.quests.FirstOrDefault(x=>x.needsDungeon||x.needsWorldBoss)??v.quests.FirstOrDefault();return new LifecycleStep36{questId=q==null?0:q.id,questName=q==null?null:q.name,mapId=q==null?0:q.mapId,missionId=q==null?0:q.missionId,roomType=q==null?0:q.roomType,hardLevel=q==null?-1:q.hardLevel,needsDungeon=q!=null&&q.needsDungeon,needsWorldBoss=q!=null&&q.needsWorldBoss,forgeBusy=v.forgeBusy,state=state};}
  static object[] Capabilities(){string[] keys={"Compose","Fusion","Melt","Inlay","Exercise","Pet","Card","Wing","Totem","Bead","Rune","Ghost","FineStoreSuit","Latent","Spirit","Gem","zhanling","Necklace","Hole","Texp"};var types=typeof(GamePlayer).Assembly.GetTypes().Where(t=>t.Namespace!=null&&t.Namespace.StartsWith("Game.Server.Packets.Client")).Select(t=>t.Name).ToArray();var result=new List<object>{new{system="quest",verified=true,executable=true,runtimeVerified=false,evidence="QuestInventory.AddQuest/Finish；原生条件与奖励；待实机接取/领奖验收"},new{system="strengthen",verified=true,executable=true,runtimeVerified=false,evidence="同包AS3：49搬运→StoreBag12；59单bool原生强化；待实机消费验收"},new{system="texp",verified=true,executable=true,runtimeVerified=false,evidence="同包AS3：49药水×1→StoreBag12/0；99三个int；待实机消费验收"}};foreach(string action in new[]{"pet_equip","pet_feed","card_equip","card_upgrade","compose","inlay","totem_activate","necklace_cast"})result.Add(new{system=action,verified=true,executable=true,runtimeVerified=false,evidence="同包AS3精确原生包与实际背包前提已核验，待实机资源/结果验收"});foreach(string key in keys){var found=types.Where(n=>n.IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0).ToArray();if(found.Length>0)result.Add(new{system=key,verified=false,executable=false,runtimeVerified=false,evidence=String.Join(",",found)});}return result.ToArray();}
  public sealed class ForgeSession {
   [ScriptIgnore] public GamePlayer owner;
   public int version=1,userId,itemId,originalPlace,originalLevel,stoneTemplate,parameter,resourceItemId,equipmentSlot=5;public bool attempted,returning;public string last,kind="strengthen";
   public Dictionary<string,int> ownedSlots=new Dictionary<string,int>();
   public int pendingToSlot=-1,pendingTemplate,pendingSourceItem,pendingSourceCount,pendingCount,pendingFromBag,pendingFromSlot;
  }
  static readonly Dictionary<int,ForgeSession> Forges=new Dictionary<int,ForgeSession>();
  static readonly HashSet<int> ReadForges=new HashSet<int>();
  static string ForgePath(int id){return Path.Combine(Paths36.Root,"config","bot-forge-state-"+id+".json");}
  static void SaveForge(ForgeSession s){string path=ForgePath(s.userId),temp=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(temp,new JavaScriptSerializer().Serialize(s));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  static void ClearForge(GamePlayer p){string path=ForgePath(p.PlayerId);if(File.Exists(path))File.Delete(path);Forges.Remove(p.PlayerId);}
  static ForgeSession LoadForge(GamePlayer p){ForgeSession s;if(Forges.TryGetValue(p.PlayerId,out s)){if(s.userId==p.PlayerId)s.owner=p;return s;}if(ReadForges.Contains(p.PlayerId))return null;ReadForges.Add(p.PlayerId);string path=ForgePath(p.PlayerId);if(!File.Exists(path))return null;try{s=new JavaScriptSerializer().Deserialize<ForgeSession>(File.ReadAllText(path));if(s==null||s.userId!=p.PlayerId||s.version!=1||s.ownedSlots==null)throw new InvalidDataException("Forge ownership record invalid");s.owner=p;Forges.Add(p.PlayerId,s);return s;}catch{ReadForges.Remove(p.PlayerId);throw;}}
  static void TrackMove(GamePlayer p,ForgeSession s,Action<GSPacketIn> receive,ItemInfo source,int fromBag,int toSlot){s.pendingToSlot=toSlot;s.pendingTemplate=source.TemplateID;s.pendingSourceItem=source.ItemID;s.pendingSourceCount=source.Count;s.pendingCount=1;s.pendingFromBag=fromBag;s.pendingFromSlot=source.Place;SaveForge(s);Move(p,receive,fromBag,source.Place,12,toSlot,1);if(!p.SaveIntoDatabase())throw new InvalidOperationException("原生暂存保存失败，保留搬运记录等待恢复");var staged=p.StoreBag.GetItemAt(toSlot);if(staged!=null&&staged.TemplateID==s.pendingTemplate)s.ownedSlots[toSlot.ToString()]=staged.ItemID;s.pendingToSlot=-1;SaveForge(s);}
  static void ReconcileAssignedIds(GamePlayer p,ForgeSession s){
   var zeros=s.ownedSlots.Where(v=>v.Value==0).ToArray();if(zeros.Length!=1||s.pendingFromBag!=1||s.pendingCount!=1)return;
   var actual=p.StoreBag.GetItemAt(Int32.Parse(zeros[0].Key));var source=p.PropBag.GetItemAt(s.pendingFromSlot);
   if(actual!=null&&actual.ItemID>0&&actual.TemplateID==s.pendingTemplate&&actual.Count==1&&source!=null&&source.ItemID==s.pendingSourceItem&&source.Count==s.pendingSourceCount-1){s.ownedSlots[zeros[0].Key]=actual.ItemID;SaveForge(s);}
  }
  static bool ReconcilePending(GamePlayer p,ForgeSession s){ReconcileAssignedIds(p,s);if(s.pendingToSlot<0)return true;var staged=p.StoreBag.GetItemAt(s.pendingToSlot);var source=(s.pendingFromBag==0?p.MainBag:p.PropBag).GetItemAt(s.pendingFromSlot);bool sourceMoved=source==null||source.ItemID!=s.pendingSourceItem||source.Count==s.pendingSourceCount-s.pendingCount;if(staged!=null&&staged.TemplateID==s.pendingTemplate&&staged.Count==s.pendingCount&&sourceMoved){s.ownedSlots[s.pendingToSlot.ToString()]=staged.ItemID;s.pendingToSlot=-1;SaveForge(s);return true;}if(staged==null){s.pendingToSlot=-1;SaveForge(s);return true;}return false;}
  static IEnumerable<ItemInfo> Props(GamePlayer p){return Enumerable.Range(0,p.PropBag.Capalility).Select(i=>p.PropBag.GetItemAt(i)).Where(i=>i!=null&&i.IsExist&&i.Count>0);}
  static IEnumerable<ItemInfo> Equipment(GamePlayer p){return new[]{6,4,0}.Select(slot=>p.MainBag.GetItemAt(slot)).Where(i=>i!=null&&i.IsExist&&i.Count==1&&i.Template!=null&&i.Template.CanStrengthen&&i.RefineryLevel==0);}
  static int ForgePrice(){return Bussiness.GameProperties.PRICE_STRENGHTN_GOLD;}
  static ItemInfo Stone(GamePlayer p){return Props(p).Where(i=>i.Template.CategoryID==11&&i.Template.Property1==2).OrderByDescending(i=>i.Template.Property2).ThenBy(i=>i.ItemID).FirstOrDefault();}
  static ItemInfo Protection(GamePlayer p){return Props(p).FirstOrDefault(i=>i.Template.CategoryID==11&&i.Template.Property1==7);}
  static bool AnyStore(GamePlayer p){return Enumerable.Range(0,p.StoreBag.Capalility).Any(slot=>p.StoreBag.GetItemAt(slot)!=null);}
  public static bool ForgeBusy(GamePlayer p){if(p==null)return false;lock(Gate){return AnyStore(p)||LoadForge(p)!=null;}}
  static bool NativeStrengthLevel(ItemInfo item){try{return StrengthenMgr.FindStrengthenInfo(item.StrengthenLevel+1)!=null;}catch{return false;}}
  static void InspectForge(GamePlayer p,LifecycleInspection36 view,List<LifecycleAction36> actions){
   ForgeSession s=LoadForge(p);bool known=s!=null&&Object.ReferenceEquals(s.owner,p);view.forgeBusy=AnyStore(p)||known;
   var stone=Stone(p);var protection=Protection(p);int price=ForgePrice();
   view.resources=new{gold=p.PlayerCharacter.Gold,strengthenGoldCost=price,stones=Props(p).Where(i=>i.Template.CategoryID==11&&(i.Template.Property1==2||i.Template.Property1==7||i.Template.Property1==35)).Select(i=>new{itemId=i.ItemID,templateId=i.TemplateID,name=i.Template.Name,count=i.Count,kind=i.Template.Property1,power=i.Template.Property2}).ToArray(),equipment=Equipment(p).Select(i=>new{itemId=i.ItemID,templateId=i.TemplateID,name=i.Template.Name,slot=i.Place,strengthen=i.StrengthenLevel,refinery=i.RefineryLevel}).ToArray()};
   view.forge=new{busy=view.forgeBusy,managed=known,kind=known?s.kind:null,itemId=known?s.itemId:0,originalLevel=known?s.originalLevel:0,attempted=known&&s.attempted,returning=known&&s.returning,state=known?s.last:AnyStore(p)?"发现非本执行器暂存物品，等待人工处理":"空闲",nextStoneTemplate=stone==null?0:stone.TemplateID,protectionAvailable=protection!=null};
   if(view.forgeBusy){actions.Clear();if(known)actions.Add(new LifecycleAction36{action=s.kind,itemId=s.itemId,parameter=s.parameter,resourceItemId=s.resourceItemId,description=s.attempted||s.returning?"归还原生暂存装备/剩余材料":"继续同一原生事务；每次一步"});return;}
   if(stone!=null&&price>0&&p.PlayerCharacter.Gold>=price)foreach(var i in Equipment(p).Where(i=>i.StrengthenLevel<12&&NativeStrengthLevel(i)&&(i.StrengthenLevel<5||protection!=null)))actions.Add(new LifecycleAction36{action="strengthen",itemId=i.ItemID,description="原生强化 "+i.Template.Name+" +"+i.StrengthenLevel+"→+"+(i.StrengthenLevel+1)+"；材料"+stone.TemplateID+"×1；金币"+price+(i.StrengthenLevel>=5?"；保护符×1":"")+"；概率由原服决定"});
  }
  static LifecycleStep36 ForgeResult(ForgeSession s,string state,bool acted){s.last=state;return new LifecycleStep36{itemId=s.itemId,action=s.kind,forgeBusy=true,acted=acted,state=state};}
  static void Move(GamePlayer p,Action<GSPacketIn> receive,int fromBag,int fromSlot,int toBag,int toSlot,int count){var packet=new GSPacketIn(49,p.PlayerId);packet.WriteByte((byte)fromBag);packet.WriteInt(fromSlot);packet.WriteByte((byte)toBag);packet.WriteInt(toSlot);packet.WriteInt(count);packet.WriteBoolean(false);packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);}
  // Recovery is maintenance, never a strengthening decision. At most one native
  // return packet is issued, and only for a persisted owned item identity.
  public static LifecycleStep36 CancelForge(GamePlayer p,Action<GSPacketIn> receive){if(p==null)throw new ArgumentNullException("p");lock(Gate){var s=LoadForge(p);if(s==null)return new LifecycleStep36{forgeBusy=AnyStore(p),state=AnyStore(p)?"未知暂存物品，维护不作修改":"没有待撤销的强化流程"};if(receive==null)return ForgeResult(s,"没有原生收包入口，无法归还",false);if(p.CurrentRoom!=null)return ForgeResult(s,"仍在房间中，等待退出后归还",false);s.returning=true;SaveForge(s);return ReturnForge(p,s,receive);}}
  static LifecycleStep36 ReturnForge(GamePlayer p,ForgeSession s,Action<GSPacketIn> receive){ReconcileAssignedIds(p,s);
   if(!ReconcilePending(p,s))return ForgeResult(s,"未完成搬运的原生物品身份无法确认，保留记录等待人工核验",false);
   foreach(string key in s.ownedSlots.Keys.OrderBy(x=>x==s.equipmentSlot.ToString()?0:1).ThenBy(x=>Int32.Parse(x)).ToArray()){
    int slot=Int32.Parse(key);var item=p.StoreBag.GetItemAt(slot);if(item==null){s.ownedSlots.Remove(key);SaveForge(s);continue;}if(item.ItemID!=s.ownedSlots[key])return ForgeResult(s,"暂存物品身份变化，仅保留owned记录，不触碰未知物品",false);
    int destination=slot==s.equipmentSlot?0:(int)item.Template.BagType;int place=slot==s.equipmentSlot?s.originalPlace:-1;if(slot==s.equipmentSlot&&p.MainBag.GetItemAt(place)!=null)return ForgeResult(s,"原装备位置被占用，等待人工处理以免交换其他物品",false);
    Move(p,receive,12,slot,destination,place,item.Count);bool moved=p.StoreBag.GetItemAt(slot)==null;if(moved){s.ownedSlots.Remove(key);SaveForge(s);p.UpdateProperties();p.UpdateFightPower();}return ForgeResult(s,moved?"原生49已归还一项本模块owned暂存物品":"原生49未确认归还（背包空间/锁定检查），保留原记录",moved);
   }
   ClearForge(p);return new LifecycleStep36{itemId=s.itemId,action="strengthen_cancel",forgeBusy=AnyStore(p),state=AnyStore(p)?"本模块owned暂存已清理，仍有未知物品待人工处理":"本次原生强化/撤销归还完成",acted=false};
  }
  static LifecycleStep36 Forge(GamePlayer p,int max,Action<GSPacketIn> receive,int itemId,LifecycleInspection36 view){
   if(receive==null)return Need(view,"没有原生收包入口，未执行强化");if(max<0||max>12)return Need(view,"强化上限必须为0至12");ForgeSession s=LoadForge(p);
   if(s!=null&&s.kind!="strengthen")return Need(view,"当前暂存事务属于"+s.kind+"，不能用强化动作推进");
   if(s!=null&&!Object.ReferenceEquals(s.owner,p))return Need(view,"角色实例已变化，需人工确认上次暂存装备");
   if(s==null){if(AnyStore(p))return Need(view,"铁匠铺有非本执行器暂存物品，未执行修改");var equipment=Equipment(p).FirstOrDefault(i=>(itemId==0||i.ItemID==itemId)&&i.StrengthenLevel<max&&NativeStrengthLevel(i));var stone=Stone(p);if(equipment==null)return Need(view,"所选装备不支持普通强化或已达上限");if(stone==null||ForgePrice()<=0||p.PlayerCharacter.Gold<ForgePrice())return Need(view,"缺少真实强化石/金币");if(equipment.StrengthenLevel>=5&&Protection(p)==null)return Need(view,"强化+5及以上需背包中的真实保护符");
    s=new ForgeSession{owner=p,userId=p.PlayerId,itemId=equipment.ItemID,originalPlace=equipment.Place,originalLevel=equipment.StrengthenLevel,stoneTemplate=stone.TemplateID,last="准备原生强化"};Forges.Add(p.PlayerId,s);TrackMove(p,s,receive,equipment,0,5);var target=p.StoreBag.GetItemAt(5);bool moved=target!=null&&target.ItemID==s.itemId;if(!moved&&!AnyStore(p))ClearForge(p);return ForgeResult(s,moved?"原生49已将所选装备暂存，等待下一次LLM许可":"原生49未确认装备暂存，未强化",moved);
   }
   if(itemId!=0&&itemId!=s.itemId)return ForgeResult(s,"当前有另一件装备的暂存流程，拒绝改换目标",false);
   if(!ReconcilePending(p,s))return ForgeResult(s,"上次搬运身份未确认，拒绝继续强化",false);var current=p.StoreBag.GetItemAt(5);if(!s.attempted&&!s.returning&&(current==null||current.ItemID!=s.itemId||current.StrengthenLevel!=s.originalLevel)){s.returning=true;SaveForge(s);return ForgeResult(s,"装备状态变化，等待LLM许可归还，未强化",false);}
   if(s.attempted||s.returning){
    return ReturnForge(p,s,receive);
   }
   for(int slot=0;slot<p.StoreBag.Capalility;slot++){var actual=p.StoreBag.GetItemAt(slot);if(actual==null)continue;int owned;if(!s.ownedSlots.TryGetValue(slot.ToString(),out owned)||owned!=actual.ItemID||(slot!=0&&slot!=3&&slot!=5)){s.returning=true;SaveForge(s);return ForgeResult(s,"发现未知暂存物品或身份变化，禁止强化消耗，等待归还owned项",false);}}
   if(current.StrengthenLevel>=max||p.PlayerCharacter.Gold<ForgePrice()){s.returning=true;SaveForge(s);return ForgeResult(s,"目标上限/金币已变化，等待归还",false);}
   var stagedStone=p.StoreBag.GetItemAt(0);if(stagedStone==null){var stone=Stone(p);if(stone==null){s.returning=true;SaveForge(s);return ForgeResult(s,"强化石已不可用，等待归还",false);}TrackMove(p,s,receive,stone,1,0);return ForgeResult(s,"原生49暂存真实强化石×1",true);}
   if(stagedStone.Template.CategoryID!=11||stagedStone.Template.Property1!=2){s.returning=true;return ForgeResult(s,"暂存材料类型变化，取消强化并等待归还",false);}
   if(current.StrengthenLevel>=5&&p.StoreBag.GetItemAt(3)==null){var protection=Protection(p);if(protection==null){s.returning=true;SaveForge(s);return ForgeResult(s,"真实保护符已不可用，等待归还",false);}TrackMove(p,s,receive,protection,1,3);return ForgeResult(s,"原生49暂存真实保护符×1",true);}
   var charm=p.StoreBag.GetItemAt(3);if(charm!=null&&(charm.Template.CategoryID!=11||charm.Template.Property1!=7)){s.returning=true;return ForgeResult(s,"保护符类型变化，等待归还",false);}
   // Mark before native dispatch: an exception must never retry resource consumption.
   int goldBefore=p.PlayerCharacter.Gold,stoneBefore=stagedStone.Count,levelBefore=current.StrengthenLevel;
   s.attempted=true;SaveForge(s);var attempt=new GSPacketIn(59,p.PlayerId);attempt.WriteBoolean(false);attempt.Offset=GSPacketIn.HDR_SIZE;receive(attempt);foreach(string slot in s.ownedSlots.Keys.ToArray()){var actual=p.StoreBag.GetItemAt(Int32.Parse(slot));if(actual==null)s.ownedSlots.Remove(slot);else s.ownedSlots[slot]=actual.ItemID;}SaveForge(s);bool consumed=p.PlayerCharacter.Gold<goldBefore||p.StoreBag.GetItemAt(0)==null||p.StoreBag.GetItemAt(0).Count<stoneBefore||current.StrengthenLevel!=levelBefore;
   p.UpdateProperties();p.UpdateFightPower();return ForgeResult(s,consumed?"原生强化已消耗资源；等级 "+levelBefore+"→"+current.StrengthenLevel+"，金币 "+goldBefore+"→"+p.PlayerCharacter.Gold+"；等待归还":"原生强化未消费材料/金币，未记为成功；等待归还",consumed);
  }
  static readonly string[] TexpFields={"hpTexpExp","attTexpExp","defTexpExp","spdTexpExp","lukTexpExp"};
  static readonly string[] TexpNames={"血量","攻击","防御","敏捷","幸运"};
  static int TexpExp(GamePlayer p,int type){return Read<int>(p.PlayerCharacter.Texp,TexpFields[type],0);}
  static int TexpLimit(GamePlayer p){return Math.Min(p.PlayerCharacter.Grade+5,ExerciseMgr.GetMaxLevel());}
  static int TexpLevel(int exp){int result=0;for(int level=1;level<=ExerciseMgr.GetMaxLevel();level++){var row=ExerciseMgr.FindExercise(level);if(row!=null&&exp>=row.GP)result=Math.Max(result,row.Grage);}return result;}
  static void InspectTexp(GamePlayer p,LifecycleInspection36 view,List<LifecycleAction36> actions){
   var info=p.PlayerCharacter.Texp;var pills=Props(p).Where(i=>i.Template.CategoryID==20).ToArray();view.nativeSystems=new Dictionary<string,object>{{"texp",info==null?null:(object)new{dailyCount=info.texpCount,dailyDate=info.texpTaskDate,attributes=Enumerable.Range(0,5).Select(type=>new{type,name=TexpNames[type],exp=TexpExp(p,type),level=TexpLevel(TexpExp(p,type)),levelLimit=TexpLimit(p)}).ToArray()}},{"texpMaterials",pills.Select(i=>new{itemId=i.ItemID,templateId=i.TemplateID,name=i.Template.Name,count=i.Count,exp=i.Template.Property2}).ToArray()}};
   if(view.forgeBusy||info==null)return;foreach(var pill in pills)for(int type=0;type<5;type++)if(TexpLevel(TexpExp(p,type))<TexpLimit(p))actions.Add(new LifecycleAction36{action="texp",itemId=pill.ItemID,parameter=type,description="原生修炼"+TexpNames[type]+"："+pill.Template.Name+"×1；当前经验"+TexpExp(p,type)+"；原生检查等级/每日限制"});
  }
  static LifecycleStep36 ExecuteTexp(GamePlayer p,LifecycleAction36 action,Action<GSPacketIn> receive,LifecycleInspection36 view){
   if(receive==null)return Need(view,"没有原生收包入口，未执行修炼");var s=LoadForge(p);if(s==null){if(AnyStore(p))return Need(view,"有未知暂存物品，未执行修炼");var pill=Props(p).FirstOrDefault(i=>i.ItemID==action.itemId&&i.Template.CategoryID==20);if(pill==null||action.parameter<0||action.parameter>4||p.PlayerCharacter.Texp==null||TexpLevel(TexpExp(p,action.parameter))>=TexpLimit(p))return Need(view,"修炼材料/等级前提已变化");s=new ForgeSession{owner=p,userId=p.PlayerId,itemId=pill.ItemID,originalPlace=pill.Place,originalLevel=TexpExp(p,action.parameter),kind="texp",parameter=action.parameter,last="准备原生修炼"};Forges.Add(p.PlayerId,s);TrackMove(p,s,receive,pill,1,0);return ForgeResult(s,"原生49暂存真实修炼药水×1，等待下一动作许可",true);}
   if(s.kind!="texp"||s.itemId!=action.itemId||s.parameter!=action.parameter)return Need(view,"当前原生暂存事务与所选修炼目标不符");if(!ReconcilePending(p,s))return ForgeResult(s,"修炼搬运身份未确认，等待维护归还",false);if(s.attempted||s.returning)return ReturnForge(p,s,receive);
   int owned;var current=p.StoreBag.GetItemAt(0);if(current==null||!s.ownedSlots.TryGetValue("0",out owned)||owned!=current.ItemID||current.Template.CategoryID!=20||current.Count!=1||Enumerable.Range(1,p.StoreBag.Capalility-1).Any(slot=>p.StoreBag.GetItemAt(slot)!=null)){s.returning=true;SaveForge(s);return ForgeResult(s,"修炼暂存材料状态变化，取消消耗并等待归还",false);}
   if(p.PlayerCharacter.Texp==null||TexpLevel(TexpExp(p,s.parameter))>=TexpLimit(p)){s.returning=true;SaveForge(s);return ForgeResult(s,"修炼等级前提变化，等待归还",false);}s.attempted=true;SaveForge(s);var packet=new GSPacketIn(99,p.PlayerId);packet.WriteInt(s.parameter);packet.WriteInt(current.TemplateID);packet.WriteInt(current.Place);packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);if(p.StoreBag.GetItemAt(0)==null)s.ownedSlots.Remove("0");SaveForge(s);p.UpdateProperties();p.UpdateFightPower();return ForgeResult(s,"原生99已提交一次修炼；"+TexpNames[s.parameter]+"经验 "+s.originalLevel+"→"+TexpExp(p,s.parameter)+"；消耗/每日限制由原服判定",true);
  }
  static UserPetInfo[] Pets(GamePlayer p){return (p.PetBag.GetPets()??new UserPetInfo[0]).Where(x=>x!=null&&x.IsExit).ToArray();}
  static readonly Dictionary<int,DateTime> PetReads=new Dictionary<int,DateTime>();
  static readonly Dictionary<int,DateTime> PetPaidReads=new Dictionary<int,DateTime>();
  static int PetRefreshCost(){return Int32.Parse(PetMgr.FindConfig("AdoptRefereshCost").Value);}
  static ItemInfo PetRefreshTicket(GamePlayer p){int id=Int32.Parse(PetMgr.FindConfig("FreeRefereshID").Value);return Props(p).FirstOrDefault(i=>i.TemplateID==id);}
  static UserPetInfo[] AdoptPets(GamePlayer p){return Enumerable.Range(0,p.PetBag.ACapalility).Select(slot=>p.PetBag.GetAdoptPetAt(slot)).Where(x=>x!=null&&x.Place>=0).ToArray();}
  static LifecycleStep36 ExecutePetAdopt(GamePlayer p,LifecycleAction36 a,Action<GSPacketIn> receive,LifecycleInspection36 view){
   if(receive==null||p.PlayerCharacter.Grade<25||Pets(p).Length>0)return Need(view,"宠物领养前提已变化");var packet=new GSPacketIn(68,p.PlayerId);
   if(a.action=="pet_refresh"){bool refresh=a.parameter==1;DateTime last;if(refresh&&(PetPaidReads.TryGetValue(p.PlayerId,out last)&&(DateTime.UtcNow-last).TotalMinutes<15||PetRefreshTicket(p)==null&&p.PlayerCharacter.Money<PetRefreshCost()))return Need(view,"缺少真实宠物刷新券/点券，或仍在刷新冷却");PetReads[p.PlayerId]=DateTime.UtcNow;if(refresh)PetPaidReads[p.PlayerId]=DateTime.UtcNow;packet.WriteByte(5);packet.WriteBoolean(refresh);}
   else{var pet=AdoptPets(p).FirstOrDefault(x=>x.Place==a.parameter&&x.ID==a.itemId);if(pet==null)return Need(view,"原生花园宠物已变化");packet.WriteByte(6);packet.WriteInt(pet.Place);}
   var messages=new List<string>();var bot=p as BotPlayer;Action<GSPacketIn> previous=bot==null?null:bot.BotClient.PacketObserver;
   if(bot!=null)bot.BotClient.PacketObserver=message=>{if(previous!=null)previous(message);if(message.Code==(short)Game.Server.Packets.ePackageType.SYS_MESSAGE){try{var copy=new GSPacketIn((byte[])message.Buffer.Clone(),message.Length);copy.Offset=GSPacketIn.HDR_SIZE;copy.ReadInt();messages.Add(copy.ReadString());}catch{}}};
   try{packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);}finally{if(bot!=null)bot.BotClient.PacketObserver=previous;}bool changed=a.action=="pet_refresh"?AdoptPets(p).Length>0:Pets(p).Length>0;
   return new LifecycleStep36{action=a.action,acted=changed,state=(a.action=="pet_refresh"?"原生宠物花园已读取；可领养数量"+AdoptPets(p).Length:"原生宠物领养"+(changed?"已完成":"未通过原服条件，未伪造宠物"))+String.Join("；",messages)};
  }
  static UserCardInfo[] Cards(GamePlayer p){return p.CardBag.GetRawSpaces().Values.Where(c=>c!=null&&c.Count>0).ToArray();}
  static void InspectPetCard(GamePlayer p,LifecycleInspection36 view,List<LifecycleAction36> actions){
   var pets=Pets(p);var cards=Cards(p);var foods=Props(p).Where(i=>i.Template.CategoryID==34&&i.TemplateID!=334100).ToArray();var systems=(Dictionary<string,object>)view.nativeSystems;
   systems["pets"]=pets.Select(x=>new{itemId=x.ID,templateId=x.TemplateID,name=x.Name,place=x.Place,level=x.Level,gp=x.GP,hunger=x.Hunger,equipped=x.IsEquip,maxLevelByGrade=p.PetBag.MaxLevelByGrade}).ToArray();systems["petFood"]=foods.Select(x=>new{itemId=x.ItemID,templateId=x.TemplateID,name=x.Template.Name,count=x.Count}).ToArray();systems["cards"]=cards.Select(c=>{var rule=CardMgr.GetCardUpdateCondition(c.Level+1);return new{itemId=c.CardID,templateId=c.TemplateID,place=c.Place,count=c.Count,level=c.Level,exp=c.CardGP,upgradeCount=rule==null?0:rule.UpdateCardCount};}).ToArray();
   if(view.forgeBusy)return;
   foreach(int template in NativePetAdopt36.Choices(p))actions.Add(new LifecycleAction36{action="pet_adopt",itemId=template,parameter=-1,description="原生后端领养首只宠物：模板"+template+"；优先消耗真实刷新券，否则原生点券费用"+NativePetAdopt36.Cost()});
   foreach(var pet in pets){if(!pet.IsEquip)actions.Add(new LifecycleAction36{action="pet_equip",itemId=pet.ID,parameter=pet.Place,description="原生宠物出战："+pet.Name+" Lv"+pet.Level});if(pet.Level<p.PetBag.MaxLevelByGrade||pet.Hunger<10000)foreach(var food in foods)actions.Add(new LifecycleAction36{action="pet_feed",itemId=pet.ID,parameter=pet.Place,resourceItemId=food.ItemID,description="原生宠物喂养："+pet.Name+"；"+food.Template.Name+"×1"});}
   foreach(var card in cards){var rule=CardMgr.GetCardUpdateCondition(card.Level+1);if(card.Level<30&&rule!=null&&rule.UpdateCardCount>0&&card.Count>=rule.UpdateCardCount)actions.Add(new LifecycleAction36{action="card_upgrade",itemId=card.CardID,parameter=card.Place,description="原生卡牌升级：模板"+card.TemplateID+" Lv"+card.Level+"；消耗重复卡牌"+rule.UpdateCardCount+"，随机经验由原服决定"});
    if(card.Place<5)continue;var template=ItemMgr.FindItemTemplate(card.TemplateID);if(template==null)continue;foreach(int slot in template.Property8==1?new[]{0}:new[]{1,2}){var equipped=p.CardBag.GetItemAt(slot);if(equipped==null||equipped.Count<0)actions.Add(new LifecycleAction36{action="card_equip",itemId=card.CardID,parameter=slot,description="原生装备卡牌到空位"+slot+"：模板"+card.TemplateID});}
   }
  }
  static LifecycleStep36 ExecutePetCard(GamePlayer p,LifecycleAction36 a,Action<GSPacketIn> receive,LifecycleInspection36 view){
   if(receive==null)return Need(view,"没有原生收包入口，未执行宠物/卡牌操作");GSPacketIn packet;bool changed=false;string state="";
   if(a.action=="pet_equip"){var pet=Pets(p).FirstOrDefault(x=>x.ID==a.itemId&&x.Place==a.parameter);if(pet==null||pet.IsEquip)return Need(view,"宠物出战前提已变化");packet=new GSPacketIn(68,p.PlayerId);packet.WriteByte(17);packet.WriteInt(pet.Place);packet.WriteBoolean(true);packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);changed=pet.IsEquip;state=changed?"原生宠物已出战":"原生宠物出战拒绝，未伪造状态";}
   else{var card=Cards(p).FirstOrDefault(c=>c.CardID==a.itemId);if(card==null)return Need(view,"所选卡牌已不存在");int oldPlace=card.Place,oldGP=card.CardGP,oldCount=card.Count;packet=new GSPacketIn(216,p.PlayerId);if(a.action=="card_upgrade"){packet.WriteInt(3);packet.WriteInt(card.Place);}else{packet.WriteInt(0);packet.WriteInt(card.Place);packet.WriteInt(a.parameter);}packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);var after=Cards(p).FirstOrDefault(c=>c.CardID==a.itemId)??p.CardBag.GetItemAt(a.action=="card_equip"?a.parameter:oldPlace);changed=after!=null&&(after.Place!=oldPlace||after.CardGP!=oldGP||after.Count!=oldCount);state=changed?"原生卡牌操作完成；经验 "+oldGP+"→"+after.CardGP+"，数量 "+oldCount+"→"+after.Count:"原生卡牌操作拒绝/未变化";}
   if(changed){p.UpdateProperties();p.UpdateFightPower();}return new LifecycleStep36{itemId=a.itemId,action=a.action,acted=changed,state=state};
  }
  static LifecycleStep36 ExecutePetFeed(GamePlayer p,LifecycleAction36 a,Action<GSPacketIn> receive,LifecycleInspection36 view){
   if(receive==null)return Need(view,"没有原生收包入口，未执行宠物喂养");var pet=Pets(p).FirstOrDefault(x=>x.ID==a.itemId&&x.Place==a.parameter);var s=LoadForge(p);if(s==null){if(pet==null||AnyStore(p))return Need(view,"宠物/暂存前提已变化");var food=Props(p).FirstOrDefault(i=>i.ItemID==a.resourceItemId&&i.Template.CategoryID==34&&i.TemplateID!=334100);if(food==null)return Need(view,"所选真实宠物口粮已不存在");s=new ForgeSession{owner=p,userId=p.PlayerId,itemId=pet.ID,originalPlace=food.Place,originalLevel=pet.GP,kind="pet_feed",parameter=pet.Place,resourceItemId=food.ItemID,last="准备原生宠物喂养"};Forges.Add(p.PlayerId,s);TrackMove(p,s,receive,food,1,0);return ForgeResult(s,"原生49暂存真实宠物口粮×1",true);}
   if(s.kind!="pet_feed"||s.itemId!=a.itemId||s.resourceItemId!=a.resourceItemId)return Need(view,"当前暂存事务与所选宠物喂养不符");if(!ReconcilePending(p,s))return ForgeResult(s,"宠物口粮搬运身份未确认",false);if(s.attempted||s.returning)return ReturnForge(p,s,receive);int owned;var current=p.StoreBag.GetItemAt(0);if(pet==null||current==null||!s.ownedSlots.TryGetValue("0",out owned)||owned!=current.ItemID||current.Count!=1||current.Template.CategoryID!=34||current.TemplateID==334100||Enumerable.Range(1,p.StoreBag.Capalility-1).Any(slot=>p.StoreBag.GetItemAt(slot)!=null)){s.returning=true;SaveForge(s);return ForgeResult(s,"宠物/口粮身份变化，取消消耗等待归还",false);}
   int oldGP=pet.GP,oldHunger=pet.Hunger;s.attempted=true;SaveForge(s);var packet=new GSPacketIn(68,p.PlayerId);packet.WriteByte(4);packet.WriteInt(current.Place);packet.WriteInt(12);packet.WriteInt(pet.Place);packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);if(p.StoreBag.GetItemAt(0)==null)s.ownedSlots.Remove("0");SaveForge(s);p.UpdateProperties();p.UpdateFightPower();return ForgeResult(s,"原生68/4宠物喂养：经验 "+oldGP+"→"+pet.GP+"，饱食 "+oldHunger+"→"+pet.Hunger,pet.GP!=oldGP||pet.Hunger!=oldHunger||p.StoreBag.GetItemAt(0)==null);
  }
  static IEnumerable<ItemInfo> Wearables(GamePlayer p){return Enumerable.Range(0,31).Select(slot=>p.MainBag.GetItemAt(slot)).Where(i=>i!=null&&i.IsExist&&i.Count==1&&i.Template!=null);}
  static int ComposeValue(ItemInfo item,int type){switch(type){case 1:return item.AttackCompose;case 2:return item.DefendCompose;case 3:return item.AgilityCompose;case 4:return item.LuckCompose;default:return Int32.MaxValue;}}
  static bool CompatibleHole(ItemInfo item,ItemInfo gem,int hole){
   if(hole<1||hole>6||gem.Template.CategoryID!=11||gem.Template.Property1!=31||Read<int>(item,"Hole"+hole,-1)!=0)return false;
   string[] holes=(item.Template.Hole??"").Split('|');if(holes.Length<hole)return false;string[] values=holes[hole-1].Split(',');int required,type;if(values.Length<2||!Int32.TryParse(values[0],out required)||!Int32.TryParse(values[1],out type)||type!=gem.Template.Property2)return false;
   if(hole<=4)return item.StrengthenLevel>=required||Read<int>(item,"Hole"+hole,-1)>=0;int level=Read<int>(item,"Hole"+hole+"Level",-1);return level>0&&(gem.Template.Level+1)/2<=level;
  }
  static void InspectCraft(GamePlayer p,LifecycleInspection36 view,List<LifecycleAction36> actions){
   var stones=Props(p).Where(i=>i.Template.CategoryID==11&&(i.Template.Property1==1||i.Template.Property1==31)).ToArray();var wearables=Wearables(p).ToArray();var systems=(Dictionary<string,object>)view.nativeSystems;
   systems["craftMaterials"]=stones.Select(i=>new{itemId=i.ItemID,templateId=i.TemplateID,name=i.Template.Name,count=i.Count,kind=i.Template.Property1,attribute=i.Template.Property3,value=i.Template.Property4,holeType=i.Template.Property2}).ToArray();systems["craftEquipment"]=wearables.Where(i=>i.Template.CanCompose||new[]{1,5,7,13}.Contains(i.Template.CategoryID)).Select(i=>new{itemId=i.ItemID,name=i.Template.Name,slot=i.Place,attack=i.AttackCompose,defence=i.DefendCompose,agility=i.AgilityCompose,luck=i.LuckCompose,holes=Enumerable.Range(1,6).Select(h=>Read<int>(i,"Hole"+h,-1)).ToArray(),holeConfig=i.Template.Hole,hole5Level=i.Hole5Level,hole6Level=i.Hole6Level}).ToArray();if(view.forgeBusy)return;
   foreach(var item in wearables)foreach(var stone in stones){if(stone.Template.Property1==1&&item.Template.CanCompose&&stone.Template.Property3>=1&&stone.Template.Property3<=4&&stone.Template.Property4>ComposeValue(item,stone.Template.Property3)&&Bussiness.GameProperties.PRICE_COMPOSE_GOLD>0&&p.PlayerCharacter.Gold>=Bussiness.GameProperties.PRICE_COMPOSE_GOLD)actions.Add(new LifecycleAction36{action="compose",itemId=item.ItemID,resourceItemId=stone.ItemID,parameter=stone.Template.Property3,description="原生装备合成："+item.Template.Name+"；"+stone.Template.Name+"×1；现有属性"+ComposeValue(item,stone.Template.Property3)+"，材料档位"+stone.Template.Property4+"；成功率由原服决定"});if(stone.Template.Property1==31&&new[]{1,5,7,13}.Contains(item.Template.CategoryID)&&p.PlayerCharacter.Gold>=2000)for(int hole=1;hole<=6;hole++)if(CompatibleHole(item,stone,hole))actions.Add(new LifecycleAction36{action="inlay",itemId=item.ItemID,resourceItemId=stone.ItemID,parameter=hole,description="原生镶嵌已开启空孔"+hole+"："+item.Template.Name+"；"+stone.Template.Name+"×1；金币由原服检查"});}
  }
  static LifecycleStep36 ExecuteCraft(GamePlayer p,LifecycleAction36 a,Action<GSPacketIn> receive,LifecycleInspection36 view){
   if(receive==null)return Need(view,"没有原生收包入口，未执行合成/镶嵌");var s=LoadForge(p);if(s==null){if(AnyStore(p))return Need(view,"有未知暂存物品，未执行合成/镶嵌");var equipment=Wearables(p).FirstOrDefault(i=>i.ItemID==a.itemId);var material=Props(p).FirstOrDefault(i=>i.ItemID==a.resourceItemId);if(equipment==null||material==null)return Need(view,"所选装备/材料已变化");s=new ForgeSession{owner=p,userId=p.PlayerId,itemId=equipment.ItemID,resourceItemId=material.ItemID,parameter=a.parameter,equipmentSlot=a.action=="compose"?1:0,originalPlace=equipment.Place,originalLevel=a.action=="compose"?ComposeValue(equipment,a.parameter):Read<int>(equipment,"Hole"+a.parameter,-1),kind=a.action,last="准备原生"+a.action};Forges.Add(p.PlayerId,s);TrackMove(p,s,receive,equipment,0,s.equipmentSlot);return ForgeResult(s,"原生49暂存所选装备，等待下一动作许可",true);}
   if(s.kind!=a.action||s.itemId!=a.itemId||s.resourceItemId!=a.resourceItemId||s.parameter!=a.parameter)return Need(view,"当前暂存事务与所选合成/镶嵌目标不符");if(!ReconcilePending(p,s))return ForgeResult(s,"上次搬运身份未确认，等待维护归还",false);if(s.attempted||s.returning)return ReturnForge(p,s,receive);int owned;var current=p.StoreBag.GetItemAt(s.equipmentSlot);if(current==null||current.ItemID!=s.itemId||!s.ownedSlots.TryGetValue(s.equipmentSlot.ToString(),out owned)||owned!=current.ItemID){s.returning=true;SaveForge(s);return ForgeResult(s,"暂存装备身份变化，等待归还",false);}
   for(int slot=0;slot<p.StoreBag.Capalility;slot++){var actual=p.StoreBag.GetItemAt(slot);if(actual==null)continue;if(!s.ownedSlots.TryGetValue(slot.ToString(),out owned)||owned!=actual.ItemID||(slot!=s.equipmentSlot&&!(s.kind=="compose"&&slot==2))){s.returning=true;SaveForge(s);return ForgeResult(s,"发现未知暂存物品，禁止合成/镶嵌消耗",false);}}
   var source=Props(p).FirstOrDefault(i=>i.ItemID==s.resourceItemId);GSPacketIn packet;int gold=p.PlayerCharacter.Gold;if(s.kind=="compose"){
    var stone=p.StoreBag.GetItemAt(2);if(stone==null){if(source==null||source.Template.CategoryID!=11||source.Template.Property1!=1||source.Template.Property3!=s.parameter){s.returning=true;SaveForge(s);return ForgeResult(s,"原合成材料已不可用，等待归还",false);}TrackMove(p,s,receive,source,1,2);return ForgeResult(s,"原生49暂存真实合成石×1",true);}
    if(stone.Count!=1||stone.Template.CategoryID!=11||stone.Template.Property1!=1||stone.Template.Property3!=s.parameter||!current.Template.CanCompose||stone.Template.Property4<=ComposeValue(current,s.parameter)||gold<Bussiness.GameProperties.PRICE_COMPOSE_GOLD){s.returning=true;SaveForge(s);return ForgeResult(s,"原生合成资源/提升前提变化，等待归还",false);}packet=new GSPacketIn(58,p.PlayerId);packet.WriteBoolean(false);
   }else{if(source==null||!CompatibleHole(current,source,s.parameter)||gold<2000){s.returning=true;SaveForge(s);return ForgeResult(s,"真实宝珠/空孔/金币前提变化，等待归还",false);}packet=new GSPacketIn(121,p.PlayerId);packet.WriteInt(12);packet.WriteInt(current.Place);packet.WriteInt(s.parameter);packet.WriteInt(source.BagType);packet.WriteInt(source.Place);}
   s.attempted=true;SaveForge(s);packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);foreach(string key in s.ownedSlots.Keys.ToArray()){var actual=p.StoreBag.GetItemAt(Int32.Parse(key));if(actual==null)s.ownedSlots.Remove(key);else s.ownedSlots[key]=actual.ItemID;}SaveForge(s);p.UpdateProperties();p.UpdateFightPower();int after=s.kind=="compose"?ComposeValue(current,s.parameter):Read<int>(current,"Hole"+s.parameter,-1);return ForgeResult(s,"原生"+s.kind+"已提交一次；属性/孔位 "+s.originalLevel+"→"+after+"，金币 "+gold+"→"+p.PlayerCharacter.Gold+"；等待归还装备",after!=s.originalLevel||gold!=p.PlayerCharacter.Gold||s.kind=="compose"&&p.StoreBag.GetItemAt(2)==null);
  }
 }
}




