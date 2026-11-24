using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Local36 {
 // All changes go through the same native packets used by the 3.6 Flash client.
 public static class MailCare36 {
  static readonly ConcurrentDictionary<int,bool> Busy=new ConcurrentDictionary<int,bool>();
  static readonly ConcurrentDictionary<string,DateTime> Attempted=new ConcurrentDictionary<string,DateTime>();
  static readonly object BoxGate=new object();static HashSet<int> BoxTemplates=new HashSet<int>();static DateTime BoxRefresh=DateTime.MinValue;
  public static Action<GamePlayer,int,string> MailReceived;
  public static object InspectInventory(GamePlayer bot){
   var items=bot.MainBag.GetItems()??new List<ItemInfo>();
   return new{userId=bot.PlayerId,beginSlot=bot.MainBag.BeginSlot,equipSlots=Enumerable.Range(0,31).Where(s=>bot.MainBag.IsEquipSlot(s)).ToArray(),items=items.Where(i=>i!=null&&i.IsExist&&i.Template!=null).Take(100).Select(i=>InspectItem(bot,i)).ToArray()};
  }
  static object InspectItem(GamePlayer bot,ItemInfo item){try{return new{itemId=item.ItemID,templateId=item.TemplateID,place=item.Place,count=item.Count,canEquip=bot.CanEquip(item.Template),templateCanEquip=item.Template.CanEquip,valid=item.IsValidItem(),legal=Legal(bot,item),needLevel=item.Template.NeedLevel,needSex=item.Template.NeedSex,main=MainAttribute(item),attributes=Attributes(bot,item),validSlots=Enumerable.Range(0,31).Where(s=>bot.MainBag.CanEquipSlotContains(s,item.Template)).ToArray(),betterSlot=BetterSlot(bot,item)};}catch(Exception e){return new{itemId=item.ItemID,templateId=item.TemplateID,error=e.GetType().Name};}}
  public static string NativeTick(GamePlayer bot,Action<GSPacketIn> receive,bool allowGrowth=true){
   if(bot==null||receive==null||!bot.IsActive||bot.CurrentRoom!=null||bot.CurrentSpaRoom!=null||bot.CurrentMarryRoom!=null||bot.IsInWorldBossRoom||!Busy.TryAdd(bot.PlayerId,true))return null;
   try{
    if(Ready(bot,"scan-mail",20))using(var db=new PlayerBussiness()){
     var mails=db.GetMailByUserID(bot.PlayerId)??new UserMailInfo[0];
     foreach(var mail in mails.Where(m=>m.IsExist&&m.ReceiverID==bot.PlayerId&&!(m.Type>100&&m.Money>0)&&HasAttachments(m)).OrderBy(m=>m.ID)){
      if(!Ready(bot,"mail/"+mail.ID,120))continue;
      var packet=new GSPacketIn(113,bot.PlayerId);packet.WriteInt(mail.ID);packet.WriteByte(0);Send(packet,receive);
      var after=db.GetMailSingle(bot.PlayerId,mail.ID);
      if(after==null||!after.IsExist||AttachmentChanged(mail,after)){var callback=MailReceived;if(callback!=null)callback(bot,mail.SenderID,mail.Title??"邮件附件");}
      return "mail/"+mail.ID;
     }
    }
    // Autonomous-growth bots require a valid cached LLM policy; mail reception is independent.
    if(!allowGrowth||!Ready(bot,"scan-inventory",10))return null;
    var items=(bot.MainBag.GetItems()??new List<ItemInfo>()).Concat(bot.PropBag.GetItems()??new List<ItemInfo>()).Where(i=>i!=null&&i.IsExist&&i.Count>0&&i.Template!=null&&i.IsValidItem()).ToArray();
    foreach(var item in items.Where(i=>i.BagType==0&&i.Place>=bot.MainBag.BeginSlot&&i.Template.CanEquip&&Legal(bot,i))){
     int slot=BetterSlot(bot,item);if(slot<0||!Ready(bot,"equip/"+item.ItemID,120))continue;
     var packet=new GSPacketIn(49,bot.PlayerId);packet.WriteByte((byte)item.BagType);packet.WriteInt(item.Place);packet.WriteByte(0);packet.WriteInt(slot);packet.WriteInt(1);packet.WriteBoolean(false);Send(packet,receive);return "equip/"+item.TemplateID+"/"+slot;
    }
    foreach(var item in items.Where(i=>Legal(bot,i)&&!i.Template.CanEquip)){
     var t=item.Template;
     // 21 is the actual EXP-bottle category; 22 removes EXP and must never be auto-used.
     if(t.CanUse&&t.CategoryID==11&&(t.Property1==21||t.Property1==888)&&Ready(bot,"use/"+item.ItemID,120)){
      var packet=new GSPacketIn(183,bot.PlayerId);packet.WriteInt(item.BagType);packet.WriteInt(item.Place);packet.WriteInt(1);packet.WriteInt(item.TemplateID);packet.WriteInt(1);packet.WriteBoolean(false);packet.WriteBoolean(true);packet.WriteInt(1);Send(packet,receive);return "use/"+item.TemplateID;
     }
     if(t.CategoryID==11&&t.Property1==6&&t.Property2!=4&&!SpecialBox(t.TemplateID)&&IsConfiguredBox(t.TemplateID)&&Ready(bot,"box/"+item.ItemID,120)){
      var packet=new GSPacketIn(63,bot.PlayerId);packet.WriteByte((byte)item.BagType);packet.WriteInt(item.Place);packet.WriteInt(1);packet.WriteBoolean(false);Send(packet,receive);return "box/"+item.TemplateID;
     }
    }
    return null;
   }catch(Exception e){Console.WriteLine("BOT MAIL CARE "+bot.PlayerId+": "+e.GetType().Name);return "error/"+e.GetType().Name;}
   finally{bool unused;Busy.TryRemove(bot.PlayerId,out unused);}
  }
  static void Send(GSPacketIn packet,Action<GSPacketIn> receive){packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);}
  static bool Ready(GamePlayer bot,string action,int seconds){string key=bot.PlayerId+"/"+action;DateTime last,now=DateTime.UtcNow;if(Attempted.TryGetValue(key,out last)&&(now-last).TotalSeconds<seconds)return false;Attempted[key]=now;return true;}
  static bool Legal(GamePlayer bot,ItemInfo item){var t=item.Template;return t.NeedLevel<=bot.PlayerCharacter.Grade&&(t.NeedSex==0||t.NeedSex==(bot.PlayerCharacter.Sex?1:2));}
  static bool HasAttachments(UserMailInfo m){return m.Gold>0||m.Money>0||m.GiftToken>0||new[]{m.Annex1,m.Annex2,m.Annex3,m.Annex4,m.Annex5}.Any(x=>!String.IsNullOrEmpty(x)&&x!="0");}
  static bool AttachmentChanged(UserMailInfo a,UserMailInfo b){return a.Annex1!=b.Annex1||a.Annex2!=b.Annex2||a.Annex3!=b.Annex3||a.Annex4!=b.Annex4||a.Annex5!=b.Annex5||a.Gold!=b.Gold||a.Money!=b.Money||a.GiftToken!=b.GiftToken;}
  static int BetterSlot(GamePlayer bot,ItemInfo candidate){
   if(!bot.CanEquip(candidate.Template))return -1;
   int selected=-1;double weakest=Double.MaxValue;
   for(int slot=0;slot<bot.MainBag.BeginSlot;slot++){
    if(!bot.MainBag.IsEquipSlot(slot)||!bot.MainBag.CanEquipSlotContains(slot,candidate.Template))continue;
    var old=bot.MainBag.GetItemAt(slot);if(old==null)return slot;
    double oldMain=MainAttribute(old),newMain=MainAttribute(candidate);int oldAttrs=Attributes(bot,old),newAttrs=Attributes(bot,candidate);
    // Avoid replacing an existing item with a tradeoff or a cosmetic-only change.
    if(newMain<oldMain||newAttrs<oldAttrs||(newMain==oldMain&&newAttrs==oldAttrs))continue;
    double score=oldMain+oldAttrs;if(score<weakest){weakest=score;selected=slot;}
   }return selected;
  }
  static double MainAttribute(ItemInfo item){if(item==null||item.Template==null)return 0;bool gold=false;var flag=item.GetType().GetProperty("IsGold");if(flag!=null&&flag.PropertyType==typeof(bool))gold=(bool)flag.GetValue(item,null);return Math.Round(item.Template.Property7*Math.Pow(1.1,item.StrengthenLevel+(gold?1:0)));}
  static int Attributes(GamePlayer bot,ItemInfo item){int a=item.Attack,d=item.Defence,g=item.Agility,l=item.Luck;bot.MainBag.AddProperty(item,ref a,ref d,ref g,ref l);return a+d+g+l;}
  static bool SpecialBox(int id){return new[]{112019,112109,112047,112100,112101,11252,11257,11258,11259,11260,311500,313500,312500}.Contains(id);}
  static bool IsConfiguredBox(int template){lock(BoxGate){if(DateTime.UtcNow>=BoxRefresh){var next=new HashSet<int>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var cmd=new SqlCommand("SELECT DISTINCT DataId FROM Shop_Goods_Box",db))using(var r=cmd.ExecuteReader())while(r.Read())next.Add(r.GetInt32(0));}BoxTemplates=next;BoxRefresh=DateTime.UtcNow.AddMinutes(10);}return BoxTemplates.Contains(template);}}
 }
}
