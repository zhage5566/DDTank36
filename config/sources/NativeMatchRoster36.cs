using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using Bussiness;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 // Provision NPC-owned native items, never copy calculated combat totals.
 public static class NativeMatchRoster36 {
  public static int Build(GamePlayer reference) {
   ItemInfo[] items;int grade,totem,neck;bool sex;int[] exercise;
   lock(reference){var c=reference.PlayerCharacter;grade=c.Grade;sex=c.Sex;totem=c.TotemId;neck=c.necklaceCastLevel;
    exercise=c.Texp==null?null:new[]{c.Texp.hpTexpExp,c.Texp.attTexpExp,c.Texp.defTexpExp,c.Texp.spdTexpExp,c.Texp.lukTexpExp};
    items=Enumerable.Range(0,31).Except(new[]{2,3,5}).Select(slot=>reference.MainBag.GetItemAt(slot)).Where(i=>i!=null&&i.IsExist&&i.Count>0).Select(i=>{var copy=i.Clone();foreach(var prop in typeof(ItemInfo).GetProperties()){if(prop.CanRead&&prop.GetSetMethod()!=null&&prop.GetIndexParameters().Length==0&&(prop.PropertyType.IsValueType||prop.PropertyType==typeof(string)))prop.SetValue(copy,prop.GetValue(i,null),null);}return copy;}).ToArray();
   }
   if(!items.Any(i=>i.Place==6))throw new InvalidOperationException("Reference has no native equipped weapon");
   int id=BotCatalog.CreateBot(new NewBotRequest{name="星旅"+Guid.NewGuid().ToString("N").Substring(0,7),level=grade,sex=sex,enabled=false,hideSuit=false,randomEquipment=true});
   using(var db=new PlayerBussiness()){
    var info=db.GetUserSingleByUserID(id);var bot=new BotPlayer(info,new BotClient());
    if(!bot.LoadFromDatabase())throw new InvalidOperationException("Native NPC load failed");bot.Online=false;bot.PlayerCharacter.State=0;
    foreach(var item in items){int slot=item.Place;var old=bot.MainBag.GetItemAt(slot);if(old!=null&&!bot.MainBag.RemoveItem(old))throw new InvalidOperationException("Native seed removal refused");
     item.ItemID=0;item.UserID=id;item.Place=-1;item.BagType=0;item.IsBinds=true;
     if(!bot.MainBag.AddItemTo(item,slot))throw new InvalidOperationException("Native NPC equip refused at "+slot);
    }
    var c=bot.PlayerCharacter;if(exercise!=null&&c.Texp!=null){c.Texp.hpTexpExp=exercise[0];c.Texp.attTexpExp=exercise[1];c.Texp.defTexpExp=exercise[2];c.Texp.spdTexpExp=exercise[3];c.Texp.lukTexpExp=exercise[4];c.Texp.IsDirty=true;}
    c.TotemId=totem;c.necklaceCastLevel=neck;
    WeaponIdentity36.Normalize(bot);bot.UpdateItemForUser(null);bot.UpdateWeapon(bot.MainBag.GetItemAt(6));bot.UpdateSecondWeapon(bot.MainBag.GetItemAt(15));BotAppearance36.Refresh(bot,false);NativeStats36.Recalculate(bot);
    if(!MatchPolicy36.MemberCompatible(new MatchMember36{Level=grade,Power=reference.PlayerCharacter.FightPower},new MatchMember36{Level=c.Grade,Power=c.FightPower}))throw new InvalidOperationException("Native NPC build outside fair power band: "+c.FightPower);
    if(!bot.SaveIntoDatabase())throw new InvalidOperationException("Native NPC save refused");if(c.Texp!=null&&!db.UpdateUserTexpInfo(c.Texp))throw new InvalidOperationException("Native NPC exercise save refused");
    using(var sql=new SqlConnection(Paths36.Db)){sql.Open();using(var cmd=new SqlCommand("UPDATE Local36_Bots SET Enabled=1 WHERE UserID=@id",sql)){cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}}
    Console.WriteLine("BOT NATIVE MATCH BUILD: id="+id+" grade="+c.Grade+" power="+c.FightPower+" reference="+reference.PlayerId);
    return id;
   }
  }
 }
}
