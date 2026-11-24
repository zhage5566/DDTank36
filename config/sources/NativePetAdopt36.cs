using System;
using System.Linq;
using System.Collections.Concurrent;
using Bussiness;
using Game.Logic;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 // First-pet growth is a backend operation. PetMgr owns every generated stat,
 // inventory owns capacity, PlayerBussiness owns persistence, native methods
 // consume the existing garden refresh ticket/cost. No panel totals are written.
 public static class NativePetAdopt36 {
  static readonly ConcurrentDictionary<int,bool> Busy=new ConcurrentDictionary<int,bool>();
  public static bool IsBusy(int id){return Busy.ContainsKey(id);}
  public static int Cost(){return Int32.Parse(PetMgr.FindConfig("AdoptRefereshCost").Value);}
  static int Ticket(){return Int32.Parse(PetMgr.FindConfig("FreeRefereshID").Value);}
  static bool HasTicket(GamePlayer p){return p.PropBag.GetItems().Any(i=>i!=null&&i.IsExist&&i.Count>0&&i.TemplateID==Ticket());}
  public static int[] Choices(GamePlayer p){if(p.PlayerCharacter.Grade<25||(p.PetBag.GetPets()??new UserPetInfo[0]).Any(i=>i!=null&&i.IsExit)||(!HasTicket(p)&&p.PlayerCharacter.Money<Cost()))return new int[0];
   return (PetMgr.FindConfig("NewPet").Value??"").Split(',').Select(v=>{int id;return Int32.TryParse(v,out id)?id:0;}).Where(id=>id>0&&PetMgr.FindPetTemplate(id)!=null).Distinct().ToArray();
  }
  public static LifecycleStep36 Adopt(GamePlayer p,int template){bool reserved;if(!Choices(p).Contains(template)||!(reserved=Busy.TryAdd(p.PlayerId,true)))return new LifecycleStep36{action="pet_adopt",state="原生宠物资源/空位前提已变化"};
   try{int slot=Enumerable.Range(0,p.PetBag.Capalility).FirstOrDefault(i=>p.PetBag.GetPetAt(i)==null);if(p.PetBag.GetPetAt(slot)!=null)return new LifecycleStep36{action="pet_adopt",state="原生宠物背包已满"};
    var pet=PetMgr.CreatePet(PetMgr.FindPetTemplate(template),p.PlayerId,slot,p.PlayerCharacter.Grade,p.PlayerCharacter.VIPLevel);
    if(pet==null)throw new InvalidOperationException("Native pet factory returned no pet");pet.IsExit=true;pet.IsEquip=false;
    using(var db=new PlayerBussiness()){if(!db.AddUserAdoptPet(pet,true))throw new InvalidOperationException("Native owned pet save refused");}
    if(!p.PetBag.AddPetTo(pet,slot))throw new InvalidOperationException("Native pet inventory admission refused");
    if(HasTicket(p)){if(!p.PropBag.RemoveTemplate(Ticket(),1))throw new InvalidOperationException("Native refresh ticket consumption refused");}else if(p.RemoveMoney(Cost())!=Cost())throw new InvalidOperationException("Native pet currency consumption refused");
    p.SaveIntoDatabase();return new LifecycleStep36{action="pet_adopt",acted=true,state="原生后端已创建并保存首只宠物："+pet.Name+"；模板"+pet.TemplateID+"，位置"+slot+"；已消耗刷新券/原生点券费用"};
   }finally{bool old;Busy.TryRemove(p.PlayerId,out old);}
  }
 }
}
