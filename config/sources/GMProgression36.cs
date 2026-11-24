using System;
using System.Collections.Generic;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Bussiness;
using Bussiness.Managers;
using Game.Server.GameObjects;
using Game.Server.Managers;
using SqlDataProvider.Data;

namespace Local36 {
 public sealed class GMProgressionRequest36 {
  public int userId;public string requestId;public GMEquipmentPatch36[] equipment;public GMTexpPatch36 texp;public GMSystemPatch36 systems;
 }
 public sealed class GMEquipmentPatch36 {
  public int slot;public int? itemId,strengthen,attackCompose,defenceCompose,agilityCompose,luckCompose;
 }
 public sealed class GMTexpPatch36 {public int? hpExp,attackExp,defenceExp,agilityExp,luckExp;}
 public sealed class GMSystemPatch36 {public int? totemId,necklaceCastLevel;}
 public sealed class GMSystemState36 {
  public int totemId,necklaceCastLevel,necklaceExp,necklaceExpAdd,necklaceArtificeNeedCount,cardRecords,petRecords;
  public string totemUpgrade,totemReason,necklaceReason;public bool totemEditable,necklaceEditable;
 }
 public sealed class GMEquipmentState36 {
  public int slot,itemId,templateId,strengthen,strengthenExp,attackCompose,defenceCompose,agilityCompose,luckCompose,refinery,hole1,hole2,hole3,hole4,hole5,hole6;
  public string name;public bool validNativeSlot,canStrengthen,canCompose;
 }
 public sealed class GMTexpState36 {
  public bool available;public int hpExp,attackExp,defenceExp,agilityExp,luckExp,dailyCount,taskCount;public DateTime taskDate;
 }
 public sealed class GMProgressionState36 {
  public bool ok=true,idle,forgeBusy,online;
  public int userId,grade;public string name;
  public GMEquipmentState36[] equipment;public GMTexpState36 texp;public GMSystemState36 systems;
  public object stats,ranges,supported;
 }
 public sealed class GMProgressionResult36 {
  public bool ok,rolledBack,persistenceVerified;
  public string requestId,error,rollbackError;
  public GMProgressionState36 before,after;
 }
 public static class GMProgression36 {
  // Root may replace this with its verified complete ordinary-player path.
  // Never follow this callback with the old handwritten bot base-stat formula.
  public static Action<GamePlayer> Recalculate=DefaultRecalculate;
  sealed class Ranges {
   public int[] strengths;public readonly int[] compose=new int[4];
   public object[] strengthLookups,strengthCaches;
   public int texpMaxLevel,texpMaxExp,globalTexpMaxLevel;
   public int[] totemIds,necklaceLevels;
  }
  public static GMProgressionState36 Inspect(GamePlayer player){
   if(player==null)throw new ArgumentNullException("player");
   lock(player){RequireBot(player);return Snapshot(player,ReadRanges(player));}
  }
  public static GMProgressionResult36 Apply(GamePlayer player,GMProgressionRequest36 request){
   if(player==null||request==null)throw new ArgumentNullException();
   lock(player){
    RequireBot(player);
    if(request.userId!=player.PlayerId)throw new ArgumentException("Request userId does not match the online bot");
    if(String.IsNullOrEmpty(request.requestId)||!Regex.IsMatch(request.requestId,"^[A-Za-z0-9_-]{8,80}$"))throw new ArgumentException("requestId must be 8..80 letters, digits, underscore or hyphen");
    RequireIdle(player);
    Ranges ranges=ReadRanges(player);GMEquipmentPatch36[] patches=request.equipment??new GMEquipmentPatch36[0];
    Validate(player,request,patches,ranges);
    var result=new GMProgressionResult36{requestId=request.requestId,before=Snapshot(player,ranges)};
    var originals=patches.ToDictionary(p=>p.slot,p=>ItemState(player,p.slot));
    var originalItems=patches.ToDictionary(p=>p.slot,p=>Equipment(player,p.slot));
    GMTexpState36 originalTexp=TexpState(player);
    GMSystemState36 originalSystems=SystemState(player,ranges);
    bool texpChange=HasTexp(request.texp);
    bool systemChange=HasSystems(request.systems);
    try{
     // The lifecycle driver also locks(player) before native growth operations.
     // Repeat the room/forge check directly before the first mutation.
     RequireIdle(player);
     foreach(var patch in patches)WriteEquipment(player,patch);
     if(texpChange)WriteTexp(player,request.texp);
     if(systemChange)WriteSystems(player,request.systems);
     Refresh(player);
     RequireIdle(player);
     Persist(player,texpChange);
     VerifyStored(player,patches.Select(p=>p.slot).ToArray(),texpChange);
     if(systemChange)VerifySystems(player);
     result.ok=true;result.persistenceVerified=true;result.after=Snapshot(player,ranges);
    }catch(Exception e){
     result.error=Error(e);
     try{
      foreach(var old in originals.Values)RestoreEquipment(player,old,originalItems[old.slot]);
      if(texpChange)RestoreTexp(player,originalTexp);
      if(systemChange)RestoreSystems(player,originalSystems);
      Refresh(player);Persist(player,texpChange);
      VerifyStored(player,originals.Keys.ToArray(),texpChange);
      if(systemChange)VerifySystems(player);
      result.rolledBack=true;
     }catch(Exception rollback){result.rollbackError=Error(rollback);}
     try{result.after=Snapshot(player,ranges);}catch(Exception inspect){result.rollbackError=(result.rollbackError??"")+"; snapshot: "+Error(inspect);}
    }
    return result;
   }
  }
  static void RequireBot(GamePlayer p){
   if(!(p is BotPlayer)||!BotService.IsBot(p.PlayerId)||p.PlayerCharacter==null||p.PlayerCharacter.ID!=p.PlayerId)throw new InvalidOperationException("Only a real resident bot may be edited");
   using(var db=Open())using(var c=new SqlCommand("SELECT COUNT(*) FROM dbo.Local36_Bots WHERE UserID=@id",db)){c.Parameters.Add("@id",SqlDbType.Int).Value=p.PlayerId;if(Convert.ToInt32(c.ExecuteScalar())!=1)throw new InvalidOperationException("Bot roster membership not found");}
  }
  static bool Online(GamePlayer p){return p.IsActive&&Object.ReferenceEquals(WorldMgr.GetPlayerById(p.PlayerId),p);}
  // Root's BotAdminGate serializes isolated offline loads with pool transitions.
  // An offline loaded BotPlayer is allowed only while there is no different
  // registered object for that user. Snapshot.online still means real WorldMgr
  // registration, never merely this object's IsActive override.
  static bool Idle(GamePlayer p){var registered=WorldMgr.GetPlayerById(p.PlayerId);return (registered==null||(p.IsActive&&Object.ReferenceEquals(registered,p)))&&p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&!p.IsInWorldBossRoom;}
  static void RequireIdle(GamePlayer p){if(!Idle(p))throw new InvalidOperationException("Bot must be the active or isolated offline object, outside rooms, battles, spa, marriage and world boss");if(Lifecycle36.ForgeBusy(p))throw new InvalidOperationException("Native forge/texp staging is busy; finish or return its items first");}
  static SqlConnection Open(){var db=new SqlConnection(Paths36.Db);db.Open();return db;}
  static Ranges ReadRanges(GamePlayer p){
   var r=new Ranges();var levels=new List<int>{0};var lookups=new List<object>();
   using(var db=Open()){
    // The two tables are the original persistent level/experience definitions.
    // A missing optional manager cache must not turn a genuine level-15 table
    // into a false max=0. Report cache/key results separately, without Init/Reload.
    using(var c=new SqlCommand("SELECT DISTINCT s.StrengthenLevel FROM dbo.Item_Strengthen s JOIN dbo.StrengThenExp e ON e.Level=s.StrengthenLevel WHERE s.StrengthenLevel>0 AND s.StrengthenLevel<=100 ORDER BY s.StrengthenLevel",db))
    using(var rows=c.ExecuteReader())while(rows.Read()){
     int level=rows.GetInt32(0);levels.Add(level);
     try{var info=StrengthenMgr.FindStrengthenInfo(level);var exp=StrengthenMgr.FindStrengthenExpInfo(level);var next=StrengthenMgr.FindStrengthenExpInfo(level+1);lookups.Add(new{level=level,infoFound=info!=null,expFound=exp!=null,expLevel=exp==null?-1:exp.Level,expId=exp==null?-1:exp.ID,expAtNextKey=next!=null,nextKeyLevel=next==null?-1:next.Level});}
     catch(Exception lookup){lookups.Add(new{level=level,lookupError=lookup.GetBaseException().GetType().Name});}
    }
    using(var c=new SqlCommand("SELECT Property3,MAX(Property4) FROM dbo.Shop_Goods WHERE CategoryID=11 AND Property1=1 AND Property3 BETWEEN 1 AND 4 AND Valid=1 GROUP BY Property3",db))
    using(var rows=c.ExecuteReader())while(rows.Read())r.compose[rows.GetInt32(0)-1]=Math.Max(0,rows.GetInt32(1));
    r.totemIds=ReadIntList(db,"SELECT ID FROM dbo.Totem WHERE ID>0 ORDER BY ID");
    r.necklaceLevels=ReadIntList(db,"SELECT Level FROM dbo.TS_NecklaceCasting WHERE Level>=0 ORDER BY Level");
   }
   r.strengths=levels.Distinct().OrderBy(x=>x).ToArray();r.strengthLookups=lookups.ToArray();r.strengthCaches=StrengthCaches();
   r.globalTexpMaxLevel=ExerciseMgr.GetMaxLevel();r.texpMaxLevel=Math.Max(0,Math.Min(p.PlayerCharacter.Grade+5,r.globalTexpMaxLevel));
   if(r.texpMaxLevel>0){
    var current=ExerciseMgr.FindExercise(r.texpMaxLevel);if(current==null)throw new InvalidOperationException("Native exercise level table is unavailable");
    var next=r.texpMaxLevel<r.globalTexpMaxLevel?ExerciseMgr.FindExercise(r.texpMaxLevel+1):null;
    r.texpMaxExp=next!=null&&next.GP>current.GP?next.GP-1:current.GP;
   }
   return r;
  }
  static object[] StrengthCaches(){var result=new List<object>();foreach(var f in typeof(StrengthenMgr).GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)){if(!typeof(IDictionary).IsAssignableFrom(f.FieldType))continue;try{var d=f.GetValue(null) as IDictionary;result.Add(new{name=f.Name,initialized=d!=null,count=d==null?0:d.Count});}catch(Exception e){result.Add(new{name=f.Name,error=e.GetBaseException().GetType().Name});}}return result.ToArray();}
  static IEnumerable<int> EquipSlots(GamePlayer p){return Enumerable.Range(0,31).Where(slot=>p.MainBag.IsEquipSlot(slot)&&p.MainBag.GetItemAt(slot)!=null);}
  static ItemInfo Equipment(GamePlayer p,int slot){
   if(slot<0||slot>30||!p.MainBag.IsEquipSlot(slot))throw new ArgumentException("Not a native equipment slot");
   var item=p.MainBag.GetItemAt(slot);
   if(item==null||item.Template==null||!item.Template.CanEquip||!p.MainBag.CanEquipSlotContains(slot,item.Template)||!item.IsExist||item.Count!=1||item.ItemID<=0||item.UserID!=p.PlayerId||item.BagType!=0||item.Place!=slot)throw new ArgumentException("Slot does not hold this bot's valid native equipped item");
   return item;
  }
  static void Validate(GamePlayer p,GMProgressionRequest36 request,GMEquipmentPatch36[] patches,Ranges r){
   if(patches.Length>31||patches.Any(x=>x==null)||patches.Select(x=>x.slot).Distinct().Count()!=patches.Length)throw new ArgumentException("Use distinct native equipment slots");
   if(patches.Length==0&&!HasTexp(request.texp)&&!HasSystems(request.systems))throw new ArgumentException("No supported changes requested");
   foreach(var patch in patches){
    var item=Equipment(p,patch.slot);
    if(patch.itemId.HasValue&&patch.itemId.Value!=item.ItemID)throw new ArgumentException("Equipment changed since inspection; reload before editing");
    bool compose=patch.attackCompose.HasValue||patch.defenceCompose.HasValue||patch.agilityCompose.HasValue||patch.luckCompose.HasValue;
    if(!patch.strengthen.HasValue&&!compose)throw new ArgumentException("Equipment patch has no supported fields");
    if(patch.strengthen.HasValue&&(!item.Template.CanStrengthen||!r.strengths.Contains(patch.strengthen.Value)))throw new ArgumentException("Strengthening level is not supported by this native item/table");
    if(compose&&!item.Template.CanCompose)throw new ArgumentException("This native item cannot compose attributes");
    Check(patch.attackCompose,r.compose[0],"attackCompose");Check(patch.defenceCompose,r.compose[1],"defenceCompose");Check(patch.agilityCompose,r.compose[2],"agilityCompose");Check(patch.luckCompose,r.compose[3],"luckCompose");
   }
   if(HasTexp(request.texp)){
    if(p.PlayerCharacter.Texp==null||p.PlayerCharacter.Texp.UserID!=p.PlayerId||r.texpMaxLevel<=0)throw new ArgumentException("This bot's native Texp record/table has not been initialized");
    if(!TexpExists(p))throw new ArgumentException("Initialize the native Texp record first");
    Check(request.texp.hpExp,r.texpMaxExp,"hpExp");Check(request.texp.attackExp,r.texpMaxExp,"attackExp");Check(request.texp.defenceExp,r.texpMaxExp,"defenceExp");Check(request.texp.agilityExp,r.texpMaxExp,"agilityExp");Check(request.texp.luckExp,r.texpMaxExp,"luckExp");
   }
   if(HasSystems(request.systems)){
    // A VM-protected accessor is not trusted merely because it exists. Refuse
    // stale/default native state instead of overwriting a different DB record.
    VerifySystems(p);
    var current=SystemState(p,r);
    if(request.systems.totemId.HasValue&&(!current.totemEditable||!r.totemIds.Contains(request.systems.totemId.Value)))throw new ArgumentException("Totem requires an existing native template and no upgraded totem layers; "+current.totemReason);
    if(request.systems.necklaceCastLevel.HasValue&&(!current.necklaceEditable||!r.necklaceLevels.Contains(request.systems.necklaceCastLevel.Value)))throw new ArgumentException("Necklace casting level must exist in this server's native table");
   }
  }
  static int[] ReadIntList(SqlConnection db,string sql){var values=new List<int>();using(var c=new SqlCommand(sql,db))using(var rows=c.ExecuteReader())while(rows.Read())values.Add(rows.GetInt32(0));return values.Distinct().ToArray();}
  static bool HasSystems(GMSystemPatch36 s){return s!=null&&(s.totemId.HasValue||s.necklaceCastLevel.HasValue);}
  static GMSystemState36 SystemState(GamePlayer p,Ranges r){
   var c=p.PlayerCharacter;string upgrade=c.TotemUpgrade??"";
   bool empty=String.IsNullOrWhiteSpace(upgrade)||Regex.Replace(upgrade,@"\s","")=="{}";
   var s=new GMSystemState36{totemId=c.TotemId,totemUpgrade=upgrade,necklaceCastLevel=c.necklaceCastLevel,necklaceExp=c.necklaceExp,necklaceExpAdd=c.necklaceExpAdd,necklaceArtificeNeedCount=c.necklaceArtificeNeedCount,totemEditable=empty&&r.totemIds.Contains(c.TotemId),necklaceEditable=r.necklaceLevels.Contains(c.necklaceCastLevel)};
   s.totemReason=s.totemEditable?"Template-backed progress only; upgraded layers are preserved and not editable":empty?"Current totem ID has no native template":"Upgraded totem layers exist; direct progress editing is disabled";
   s.necklaceReason=s.necklaceEditable?"Casting level only; necklace experience and counters are preserved":"Current casting level has no native template";
   using(var db=Open())using(var q=new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.Sys_User_Card WHERE UserID=@id),(SELECT COUNT(*) FROM dbo.Sys_User_Pet WHERE UserID=@id AND IsExit=1)",db)){q.Parameters.Add("@id",SqlDbType.Int).Value=p.PlayerId;using(var row=q.ExecuteReader())if(row.Read()){s.cardRecords=row.GetInt32(0);s.petRecords=row.GetInt32(1);}}
   return s;
  }
  static void WriteSystems(GamePlayer p,GMSystemPatch36 s){var c=p.PlayerCharacter;if(s.totemId.HasValue)c.TotemId=s.totemId.Value;if(s.necklaceCastLevel.HasValue)c.necklaceCastLevel=s.necklaceCastLevel.Value;}
  static void RestoreSystems(GamePlayer p,GMSystemState36 s){var c=p.PlayerCharacter;c.TotemId=s.totemId;c.TotemUpgrade=s.totemUpgrade;c.necklaceCastLevel=s.necklaceCastLevel;c.necklaceExp=s.necklaceExp;c.necklaceExpAdd=s.necklaceExpAdd;c.necklaceArtificeNeedCount=s.necklaceArtificeNeedCount;}
  static void VerifySystems(GamePlayer p){
   using(var db=Open())using(var q=new SqlCommand("SELECT TotemId,TotemUpgrade,necklaceCastLevel,necklaceExp,necklaceExpAdd,necklaceArtificeNeedCount FROM dbo.Sys_User_Detail WHERE UserID=@id AND IsExist=1",db)){q.Parameters.Add("@id",SqlDbType.Int).Value=p.PlayerId;using(var row=q.ExecuteReader()){
    if(!row.Read())throw new InvalidOperationException("Saved player system row missing");var c=p.PlayerCharacter;
    if(row.GetInt32(0)!=c.TotemId||(row.IsDBNull(1)?"":row.GetString(1))!=(c.TotemUpgrade??"")||row.GetInt32(2)!=c.necklaceCastLevel||row.GetInt32(3)!=c.necklaceExp||row.GetInt32(4)!=c.necklaceExpAdd||row.GetInt32(5)!=c.necklaceArtificeNeedCount)throw new InvalidOperationException("Native totem/necklace persistence verification failed");
   }}
  }
  static void Check(int? value,int max,string name){if(value.HasValue&&(value.Value<0||value.Value>max))throw new ArgumentException(name+" must be between 0 and native maximum "+max);}
  static bool HasTexp(GMTexpPatch36 t){return t!=null&&(t.hpExp.HasValue||t.attackExp.HasValue||t.defenceExp.HasValue||t.agilityExp.HasValue||t.luckExp.HasValue);}
  static bool TexpExists(GamePlayer p){using(var db=Open())using(var c=new SqlCommand("SELECT COUNT(*) FROM dbo.Sys_User_Texp WHERE UserID=@id",db)){c.Parameters.Add("@id",SqlDbType.Int).Value=p.PlayerId;return Convert.ToInt32(c.ExecuteScalar())==1;}}
  static GMEquipmentState36 ItemState(GamePlayer p,int slot){
   var i=p.MainBag.GetItemAt(slot);bool valid=i.Template!=null&&i.Template.CanEquip&&i.IsExist&&i.Count==1&&i.ItemID>0&&i.UserID==p.PlayerId&&i.BagType==0&&i.Place==slot&&p.MainBag.CanEquipSlotContains(slot,i.Template);
   return new GMEquipmentState36{slot=slot,itemId=i.ItemID,templateId=i.TemplateID,name=i.Template==null?"Unknown template":i.Template.Name,strengthen=i.StrengthenLevel,strengthenExp=i.StrengthenExp,attackCompose=i.AttackCompose,defenceCompose=i.DefendCompose,agilityCompose=i.AgilityCompose,luckCompose=i.LuckCompose,refinery=i.RefineryLevel,hole1=i.Hole1,hole2=i.Hole2,hole3=i.Hole3,hole4=i.Hole4,hole5=i.Hole5,hole6=i.Hole6,validNativeSlot=valid,canStrengthen=valid&&i.Template.CanStrengthen,canCompose=valid&&i.Template.CanCompose};
  }
  static GMTexpState36 TexpState(GamePlayer p){
   var t=p.PlayerCharacter.Texp;if(t==null||t.UserID!=p.PlayerId)return new GMTexpState36();
   return new GMTexpState36{available=TexpExists(p),hpExp=t.hpTexpExp,attackExp=t.attTexpExp,defenceExp=t.defTexpExp,agilityExp=t.spdTexpExp,luckExp=t.lukTexpExp,dailyCount=t.texpCount,taskCount=t.texpTaskCount,taskDate=t.texpTaskDate};
  }
  static object Range(int max){return new{min=0,max=max};}
  static object[] TexpLevels(Ranges r){return new[]{(object)new{level=0,exp=0}}.Concat(Enumerable.Range(1,r.texpMaxLevel).Select(level=>{var row=ExerciseMgr.FindExercise(level);return row==null?null:(object)new{level=level,exp=row.GP};}).Where(x=>x!=null)).ToArray();}
  static GMProgressionState36 Snapshot(GamePlayer p,Ranges r){
   var c=p.PlayerCharacter;var texp=TexpState(p);var systems=SystemState(p,r);return new GMProgressionState36{userId=p.PlayerId,name=c.NickName,grade=c.Grade,online=Online(p),idle=Idle(p),forgeBusy=Lifecycle36.ForgeBusy(p),equipment=EquipSlots(p).Select(slot=>ItemState(p,slot)).ToArray(),texp=texp,systems=systems,stats=new{attack=c.Attack,defence=c.Defence,agility=c.Agility,luck=c.Luck,blood=c.Blood,fightPower=c.FightPower,nimbus=c.Nimbus},
    ranges=new{strengthen=new{min=0,max=r.strengths.Max(),levels=r.strengths,source="Item_Strengthen+StrengThenExp(Level)",nativeLookups=r.strengthLookups,nativeCaches=r.strengthCaches},compose=new{attack=Range(r.compose[0]),defence=Range(r.compose[1]),agility=Range(r.compose[2]),luck=Range(r.compose[3])},texp=new{min=0,max=r.texpMaxExp,maxLevel=r.texpMaxLevel,globalMaxLevel=r.globalTexpMaxLevel,levels=TexpLevels(r)},systems=new{totemId=new{levels=r.totemIds,source="Totem.ID"},necklaceCastLevel=new{levels=r.necklaceLevels,source="TS_NecklaceCasting.Level"}}},
    supported=new{equipmentStrengthen=true,equipmentCompose=true,texp=texp.available,totemProgress=systems.totemEditable,necklaceCasting=systems.necklaceEditable,derivedCombatStatsReadOnly=true,unsupported=new[]{"pet","card","totem_upgraded_layers","gem","wing","necklace_exp","ghost","refinery","direct_attack_defence_agility_luck_blood_fightpower"}}};
  }
  static void WriteEquipment(GamePlayer p,GMEquipmentPatch36 patch){
   var i=Equipment(p,patch.slot);
   if(patch.itemId.HasValue&&patch.itemId.Value!=i.ItemID)throw new InvalidOperationException("Equipment identity changed before mutation");
   if(patch.strengthen.HasValue&&i.StrengthenLevel!=patch.strengthen.Value){i.StrengthenLevel=patch.strengthen.Value;i.StrengthenExp=0;ItemInfo.OpenHole(ref i);}
   if(patch.attackCompose.HasValue)i.AttackCompose=patch.attackCompose.Value;if(patch.defenceCompose.HasValue)i.DefendCompose=patch.defenceCompose.Value;if(patch.agilityCompose.HasValue)i.AgilityCompose=patch.agilityCompose.Value;if(patch.luckCompose.HasValue)i.LuckCompose=patch.luckCompose.Value;
   i.IsDirty=true;p.MainBag.UpdateItem(i);if(patch.slot==6&&patch.strengthen.HasValue)WeaponIdentity36.Normalize(p);
  }
  static void RestoreEquipment(GamePlayer p,GMEquipmentState36 old,ItemInfo original){
   var i=Equipment(p,old.slot);if(!Object.ReferenceEquals(i,original)){if(old.slot!=6||StrengthenFamily36.Origin(i.TemplateID)!=StrengthenFamily36.Origin(old.templateId))throw new InvalidOperationException("Equipment identity changed during rollback");WeaponIdentity36.Swap(p,old.slot,i,original);i=original;}if(i.ItemID!=old.itemId||i.TemplateID!=old.templateId)throw new InvalidOperationException("Original equipment identity could not be restored");
   i.StrengthenLevel=old.strengthen;i.StrengthenExp=old.strengthenExp;i.AttackCompose=old.attackCompose;i.DefendCompose=old.defenceCompose;i.AgilityCompose=old.agilityCompose;i.LuckCompose=old.luckCompose;i.Hole1=old.hole1;i.Hole2=old.hole2;i.Hole3=old.hole3;i.Hole4=old.hole4;i.Hole5=old.hole5;i.Hole6=old.hole6;i.IsDirty=true;p.MainBag.UpdateItem(i);
  }
  static void WriteTexp(GamePlayer p,GMTexpPatch36 patch){var t=p.PlayerCharacter.Texp;if(patch.hpExp.HasValue)t.hpTexpExp=patch.hpExp.Value;if(patch.attackExp.HasValue)t.attTexpExp=patch.attackExp.Value;if(patch.defenceExp.HasValue)t.defTexpExp=patch.defenceExp.Value;if(patch.agilityExp.HasValue)t.spdTexpExp=patch.agilityExp.Value;if(patch.luckExp.HasValue)t.lukTexpExp=patch.luckExp.Value;t.IsDirty=true;}
  static void RestoreTexp(GamePlayer p,GMTexpState36 old){if(!old.available)throw new InvalidOperationException("Cannot recreate a missing original Texp record");var t=p.PlayerCharacter.Texp;t.hpTexpExp=old.hpExp;t.attTexpExp=old.attackExp;t.defTexpExp=old.defenceExp;t.spdTexpExp=old.agilityExp;t.lukTexpExp=old.luckExp;t.texpCount=old.dailyCount;t.texpTaskCount=old.taskCount;t.texpTaskDate=old.taskDate;t.IsDirty=true;}
  static void Refresh(GamePlayer p){var callback=Recalculate??DefaultRecalculate;callback(p);}
  static void DefaultRecalculate(GamePlayer p){p.MainBag.UpdatePlayerProperties();BotAppearance36.Refresh(p,false);NativeStats36.Recalculate(p);}
  static void Persist(GamePlayer p,bool texp){
   p.MainBag.SaveToDatabase();
   if(texp)using(var db=new PlayerBussiness())if(!db.UpdateUserTexpInfo(p.PlayerCharacter.Texp))throw new InvalidOperationException("Native Texp save failed");
   if(!p.SaveIntoDatabase())throw new InvalidOperationException("Native player save failed");
  }
  static void VerifyStored(GamePlayer p,int[] slots,bool texp){
   using(var db=Open()){
    foreach(int slot in slots){var i=Equipment(p,slot);using(var c=new SqlCommand("SELECT TemplateID,Place,BagType,StrengthenLevel,StrengthenExp,AttackCompose,DefendCompose,AgilityCompose,LuckCompose,Hole1,Hole2,Hole3,Hole4,Hole5,Hole6 FROM dbo.Sys_User_Goods WHERE UserID=@user AND ItemID=@item AND IsExist=1",db)){c.Parameters.Add("@user",SqlDbType.Int).Value=p.PlayerId;c.Parameters.Add("@item",SqlDbType.Int).Value=i.ItemID;using(var row=c.ExecuteReader()){int[] expected={i.TemplateID,slot,0,i.StrengthenLevel,i.StrengthenExp,i.AttackCompose,i.DefendCompose,i.AgilityCompose,i.LuckCompose,i.Hole1,i.Hole2,i.Hole3,i.Hole4,i.Hole5,i.Hole6};if(!row.Read())throw new InvalidOperationException("Saved equipment row missing");for(int n=0;n<expected.Length;n++)if(Convert.ToInt32(row[n])!=expected[n])throw new InvalidOperationException("Native equipment persistence verification failed");}}}
    if(texp)using(var c=new SqlCommand("SELECT hpTexpExp,attTexpExp,defTexpExp,spdTexpExp,lukTexpExp,texpCount,texpTaskCount,texpTaskDate FROM dbo.Sys_User_Texp WHERE UserID=@id",db)){c.Parameters.Add("@id",SqlDbType.Int).Value=p.PlayerId;using(var row=c.ExecuteReader()){var t=p.PlayerCharacter.Texp;int[] expected={t.hpTexpExp,t.attTexpExp,t.defTexpExp,t.spdTexpExp,t.lukTexpExp,t.texpCount,t.texpTaskCount};if(!row.Read())throw new InvalidOperationException("Saved Texp row missing");for(int n=0;n<expected.Length;n++)if(Convert.ToInt32(row[n])!=expected[n])throw new InvalidOperationException("Native Texp persistence verification failed");if(Math.Abs((row.GetDateTime(7)-t.texpTaskDate).TotalMilliseconds)>10)throw new InvalidOperationException("Native Texp date changed unexpectedly");}}
   }
  }
  static string Error(Exception e){string message=e.GetBaseException().GetType().Name+": "+e.GetBaseException().Message;return message.Length>500?message.Substring(0,500):message;}
 }
}
