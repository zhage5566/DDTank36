using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Bussiness.Managers;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 public sealed class NativeStatSource36 {
  public string source,evidence;public int attack,defence,agility,luck,blood;
  public object detail;
 }
 public sealed class NativeStatResult36 {
  public int userId,attack,defence,agility,luck,blood;
  public NativeStatSource36[] sources;public string[] unsupported;
  public bool applied,saved,nativeAggregation;public int originalPlayerState;
  public object before,after;
 }
 // Recalculate delegates complete aggregation to this package's native bag and
 // player methods. Describe is only a partial source audit. CombatPower36 applies
 // the explicitly selected 3.4 power policy after native aggregation. No source
 // mutation, resource creation, SQL stat write or Save is performed here.
 public static class NativeStats36 {
  static readonly object NecklaceGate=new object();static Dictionary<int,int[]> NecklaceRows;
  static object Profile(GamePlayer p){var c=p.PlayerCharacter;return new{c.Grade,c.Attack,c.Defence,c.Agility,c.Luck,c.Blood,c.FightPower,levelBlood=p.LevelPlusBlood};}
  static int[] Necklace(int level){lock(NecklaceGate){if(NecklaceRows==null){var rows=new Dictionary<int,int[]>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var cmd=new SqlCommand("SELECT Level,Attack,Defence,Agility,Luck,Blood FROM dbo.TS_NecklaceCasting",db))using(var row=cmd.ExecuteReader())while(row.Read())rows.Add(row.GetInt32(0),Enumerable.Range(1,5).Select(n=>row.GetInt32(n)).ToArray());}NecklaceRows=rows;}int[] result;if(!NecklaceRows.TryGetValue(level,out result))throw new InvalidOperationException("Native necklace casting template is missing for level "+level);return result;}}
  public static void ReloadTemplates(){lock(NecklaceGate){NecklaceRows=null;}}
  static ExerciseInfo Exercise(int exp){ExerciseInfo selected=null;for(int level=1;level<=ExerciseMgr.GetMaxLevel();level++){var row=ExerciseMgr.FindExercise(level);if(row!=null&&row.GP<=exp&&(selected==null||row.GP>selected.GP))selected=row;}return selected;}
  static NativeStatSource36 Source(string key,string evidence,int attack,int defence,int agility,int luck,int blood,object detail){return new NativeStatSource36{source=key,evidence=evidence,attack=attack,defence=defence,agility=agility,luck=luck,blood=blood,detail=detail};}
  static NativeStatResult36 DescribeCore(GamePlayer p){
   var sources=new List<NativeStatSource36>();var c=p.PlayerCharacter;
   foreach(int slot in Enumerable.Range(0,31)){
    var item=p.MainBag.GetItemAt(slot);if(item==null||!item.IsExist||item.Count<=0)continue;
    sources.Add(Source("equipment","native ItemInfo four-stat getters",item.Attack,item.Defence,item.Agility,item.Luck,0,new{slot,itemId=item.ItemID,template=item.TemplateID,strengthen=item.StrengthenLevel}));
    int attack=0,defence=0,agility=0,luck=0;p.MainBag.AddProperty(item,ref attack,ref defence,ref agility,ref luck);
    sources.Add(Source("equipment_native_holes","native MainBag.AddProperty(item,ref four stats)",attack,defence,agility,luck,0,new{slot,itemId=item.ItemID}));
   }
   if(c.Texp!=null){var t=c.Texp;var hp=Exercise(t.hpTexpExp);var a=Exercise(t.attTexpExp);var d=Exercise(t.defTexpExp);var ag=Exercise(t.spdTexpExp);var l=Exercise(t.lukTexpExp);
    sources.Add(Source("texp","native ExerciseMgr GP thresholds; same-AS3 TexpManager.getEffect fields",a==null?0:a.ExerciseA,d==null?0:d.ExerciseD,ag==null?0:ag.ExerciseAG,l==null?0:l.ExerciseL,hp==null?0:hp.ExerciseH,new{hpExp=t.hpTexpExp,attackExp=t.attTexpExp,defenceExp=t.defTexpExp,agilityExp=t.spdTexpExp,luckExp=t.lukTexpExp,hpLevel=hp==null?0:hp.Grage,attackLevel=a==null?0:a.Grage,defenceLevel=d==null?0:d.Grage,agilityLevel=ag==null?0:ag.Grage,luckLevel=l==null?0:l.Grage}));
   }
   if(c.TotemId>0){int attack=0,defence=0,agility=0,luck=0,blood=0;p.MainBag.AddBaseTotemProperty(c.TotemId,ref attack,ref defence,ref agility,ref luck,ref blood);sources.Add(Source("totem","native MainBag.AddBaseTotemProperty",attack,defence,agility,luck,blood,new{id=c.TotemId,upgrade=c.TotemUpgrade}));}
   if(c.necklaceCastLevel>0){var row=Necklace(c.necklaceCastLevel);sources.Add(Source("necklace_cast","same-package TS_NecklaceCasting row; same-AS3 NecklaceCastingInfo five attributes",row[0],row[1],row[2],row[3],row[4],new{level=c.necklaceCastLevel}));}
   var pet=p.PetBag.GetPetIsEquip();if(pet!=null&&pet.IsEquip)sources.Add(Source("equipped_pet","native UserPetInfo.TotalXX getters include native reduction and breakthrough",pet.TotalAttack,pet.TotalDefence,pet.TotalAgility,pet.TotalLuck,pet.TotalBlood,new{id=pet.ID,pet.TemplateID,pet.Level,pet.Hunger}));
   foreach(var card in p.CardBag.GetEquipCard()??new List<UserCardInfo>())if(card!=null)sources.Add(Source("equipped_card","native GetEquipCard selection; UserCardInfo.TotalXX = original + reset attributes",card.TotalAttack,card.TotalDefence,card.TotalAgility,card.TotalLuck,0,new{id=card.CardID,card.TemplateID,card.Place,card.Level,card.Count}));
   return new NativeStatResult36{userId=p.PlayerId,attack=checked(sources.Sum(v=>v.attack)),defence=checked(sources.Sum(v=>v.defence)),agility=checked(sources.Sum(v=>v.agility)),luck=checked(sources.Sum(v=>v.luck)),blood=checked(sources.Sum(v=>v.blood)),sources=sources.ToArray(),saved=false,unsupported=new[]{"latent/gemstone: AddProperty overlap not verified; no additional AddBaseLatentProperty/AddBaseGemstoneProperty call","card set buffs/special effects beyond equipped TotalXX","totem upgrade extra rates beyond native AddBaseTotemProperty","fine suit/avatar/amulet/ghost/enchant/wing/war-spirit/other progression sources not yet verified"}};
  }
  public static NativeStatResult36 Describe(GamePlayer p){if(p==null)throw new ArgumentNullException("p");lock(p){return DescribeCore(p);}}
  // The native bag aggregator returns immediately for PlayerState.Offline.
  // Bot World registration/IsActive/PlayerCharacter.State do not initialize it.
  // An isolated offline GM object may aggregate in memory, but its native state
  // is restored before its caller saves; it is never added to WorldMgr here.
  public static NativeStatResult36 Recalculate(GamePlayer p){
   if(p==null)throw new ArgumentNullException("p");lock(p){
    var original=p.PlayerState;var before=Profile(p);
    try{
     if(original==Game.Server.Packets.ePlayerState.Offline)p.PlayerState=Game.Server.Packets.ePlayerState.Online;
     p.MainBag.UpdatePlayerProperties();p.MainBag.UpdatePlayerElementProperties();p.UpdateFightPower();CombatPower36.Apply(p);p.UpdateProperties();
     var c=p.PlayerCharacter;
     return new NativeStatResult36{userId=p.PlayerId,attack=c.Attack,defence=c.Defence,agility=c.Agility,luck=c.Luck,blood=c.Blood,before=before,after=Profile(p),applied=true,saved=false,nativeAggregation=true,originalPlayerState=(int)original,sources=new[]{Source("native_complete_profile","Original PlayerEquipInventory.UpdatePlayerProperties with non-Offline PlayerState; native element/HP/damage/armor/fight-power path",c.Attack,c.Defence,c.Agility,c.Luck,c.Blood,new{playerState=(int)p.PlayerState})},unsupported=new[]{"Describe is a partial source audit only; production totals come exclusively from the original complete native aggregator"}};
    }finally{if(original==Game.Server.Packets.ePlayerState.Offline)p.PlayerState=original;}
   }
  }

 }
}
