using System;
using System.Linq;
using Game.Server.GameObjects;

namespace Local36 {
 // Derived appearance only: original inventory StrengthenLevel/IsGold data is
 // authoritative. No item, strengthening, combat stat or currency is created.
 public static class BotAppearance36 {
  static readonly int[] StyleSlots={0,1,2,3,4,5,6,11,13,14,15,16,17,18,19,20};
  public static object Inspect(GamePlayer player){var bag=player.MainBag;return new{id=player.PlayerId,nimbus=player.PlayerCharacter.Nimbus,hide=player.PlayerCharacter.Hide,style=player.PlayerCharacter.Style,items=StyleSlots.Select(slot=>{var item=bag.GetItemAt(slot);return item==null?null:new{slot,id=item.ItemID,template=item.TemplateID,strengthen=item.StrengthenLevel,category=item.Template.CategoryID,templatePic=item.Template.Pic,instancePic=item.Pic};}).Where(x=>x!=null).ToArray()};}
  public static object Refresh(GamePlayer player,bool notify){
   if(player==null)throw new ArgumentNullException("player");
   var bag=player.MainBag;int oldNimbus=player.PlayerCharacter.Nimbus;
   player.UpdateWeapon(bag.GetItemAt(6));player.UpdateSecondWeapon(bag.GetItemAt(15));
   player.UpdateHealstone(bag.GetItemAt(18));
   // Native aura and aperture rules, shared with ordinary player equipment.
   bag.GetUserNimbus();int strongest=Enumerable.Range(0,31).Select(slot=>bag.GetItemAt(slot)).Where(item=>item!=null).Select(item=>item.StrengthenLevel).DefaultIfEmpty(0).Max();player.ApertureEquip(strongest);
   string style=String.Join(",",StyleSlots.Select(slot=>{var item=bag.GetItemAt(slot);return item==null?"":item.TemplateID+"|"+item.Pic;}));
   string colors=String.Join(",",StyleSlots.Select(slot=>{var item=bag.GetItemAt(slot);return item==null?"":item.Color??"";}));
   var face=bag.GetItemAt(5);player.UpdateStyle(style,colors,face==null?"":face.Skin??"");
   if(notify)player.UpdateProperties();
   Console.WriteLine("BOT APPEARANCE: "+player.PlayerId+" nativeNimbus="+oldNimbus+"->"+player.PlayerCharacter.Nimbus+" maxStrengthen="+strongest);
   return Inspect(player);
  }
 }
}
