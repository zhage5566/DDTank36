using System;
using Bussiness.Managers;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 public static class WeaponIdentity36 {
  public static ItemInfo Normalize(GamePlayer p){var old=p.MainBag.GetItemAt(6);if(old==null||old.Template.CategoryID!=7||old.isGold)return old;int target=StrengthenFamily36.Resolve(old.TemplateID,old.StrengthenLevel);if(target==old.TemplateID)return old;var template=ItemMgr.FindItemTemplate(target);if(template==null||template.CategoryID!=old.Template.CategoryID||!template.CanEquip)throw new InvalidOperationException("Native strengthening target template is unavailable: "+target);var replacement=ItemInfo.CloneFromTemplate(template,old);if(replacement==null||replacement.TemplateID!=target||replacement.Template.TemplateID!=target)throw new InvalidOperationException("Native template clone failed");Swap(p,6,old,replacement);Console.WriteLine("BOT WEAPON IDENTITY: "+p.PlayerId+" "+old.TemplateID+" -> "+target+" strengthen="+replacement.StrengthenLevel+" pic="+replacement.Pic);return replacement;}
  // Follow original strengthen-handler identity replacement: native removal and
  // native add of CloneFromTemplate. It receives a new native ItemID on save.
  // Caller holds player lock and owns save/rollback; no SQL writes occur here.
  public static void Swap(GamePlayer p,int slot,ItemInfo current,ItemInfo replacement){var bag=p.MainBag;if(bag.GetItemAt(slot)!=current)throw new InvalidOperationException("Equipment identity changed before native replacement");bag.BeginChanges();try{if(!bag.RemoveItem(current))throw new InvalidOperationException("Native equipment removal refused");if(!bag.AddItemTo(replacement,slot)){if(!bag.AddItemTo(current,slot))throw new InvalidOperationException("Native replacement and restoration both failed");throw new InvalidOperationException("Native equipment replacement refused");}}finally{bag.CommitChanges();}}
 }
}
