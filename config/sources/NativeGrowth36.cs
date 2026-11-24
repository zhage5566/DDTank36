using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 // Same-package AS3: TotemManager.getNextInfoById uses Point + 1;
 // TotemLeftWindowActiveView sends 136(false,false) without optional insurance.
 // NecklaceCastingView uses CURRENT level costs, PropBag 11160 and 12240;
 // GameSocketOut.sendNecklaceCast sends 661 with no body.
 public static class NativeGrowth36 {
  static readonly object Gate=new object();static Dictionary<int,int> TotemByPoint;static Dictionary<int,int[]> NecklaceCosts;
  static void Templates(){lock(Gate){if(TotemByPoint!=null&&NecklaceCosts!=null)return;var totem=new Dictionary<int,int>();var necklace=new Dictionary<int,int[]>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var c=new SqlCommand("SELECT ID,Point FROM Totem ORDER BY Point",db))using(var r=c.ExecuteReader())while(r.Read())totem.Add(r.GetInt32(1),r.GetInt32(0));using(var c=new SqlCommand("SELECT Level,NeedItemCount1,NeedItemCount2 FROM TS_NecklaceCasting ORDER BY Level",db))using(var r=c.ExecuteReader())while(r.Read())necklace.Add(r.GetInt32(0),new[]{r.GetInt32(1),r.GetInt32(2)});}TotemByPoint=totem;NecklaceCosts=necklace;}}
  static int Count(GamePlayer p,int template){return Enumerable.Range(0,p.PropBag.Capalility).Select(i=>p.PropBag.GetItemAt(i)).Where(i=>i!=null&&i.IsExist&&i.TemplateID==template&&i.Count>0).Sum(i=>i.Count);}
  static TotemInfo NextTotem(GamePlayer p){Templates();int point=0;if(p.PlayerCharacter.TotemId>0){var current=TotemMgr.FindTotemInfo(p.PlayerCharacter.TotemId);if(current==null)return null;point=current.Point;}int id;return TotemByPoint.TryGetValue(point+1,out id)?TotemMgr.FindTotemInfo(id):null;}
  static bool AllowedTotem(GamePlayer p,TotemInfo next){return p.PlayerCharacter.Grade>=22&&next!=null&&next.ConsumeHonor>=0&&next.ConsumeExp>=0&&next.Random>0&&next.Random<=100&&p.PlayerCharacter.myHonor>=next.ConsumeHonor&&p.PlayerCharacter.Money>=next.ConsumeExp;}
  static bool AllowedNecklace(GamePlayer p,int[] cost){var necklace=p.MainBag.GetItemAt(12);return necklace!=null&&necklace.IsExist&&necklace.Count==1&&cost!=null&&cost[0]>=0&&cost[1]>=0&&(cost[0]>0||cost[1]>0)&&NecklaceCosts.ContainsKey(p.PlayerCharacter.necklaceCastLevel+1)&&Count(p,11160)>=cost[0]&&Count(p,12240)>=cost[1];}
  public static void Inspect(GamePlayer p,LifecycleInspection36 view,List<LifecycleAction36> actions){Templates();var c=p.PlayerCharacter;var next=NextTotem(p);int[] costs;NecklaceCosts.TryGetValue(c.necklaceCastLevel,out costs);var data=view.nativeSystems as Dictionary<string,object>;if(data==null){data=new Dictionary<string,object>();view.nativeSystems=data;}data["totemGrowth"]=new{current=c.TotemId,honor=c.myHonor,money=c.Money,next=next==null?null:(object)new{id=next.ID,point=next.Point,honorCost=next.ConsumeHonor,moneyCost=next.ConsumeExp,successPercent=next.Random,optionalInsurance=false},gradeRequired=22};data["necklaceGrowth"]=new{level=c.necklaceCastLevel,wearing=p.MainBag.GetItemAt(12)!=null,cost=costs,materials=new[]{new{template=11160,count=Count(p,11160)},new{template=12240,count=Count(p,12240)}}};if(view.forgeBusy)return;
   if(AllowedTotem(p,next))actions.Add(new LifecycleAction36{action="totem_activate",parameter=next.ID,description="原生图腾下一点 "+next.ID+"；荣誉 "+next.ConsumeHonor+"，点券 "+next.ConsumeExp+"；成功率 "+next.Random+"%，不购买额外保护"});
   if(AllowedNecklace(p,costs))actions.Add(new LifecycleAction36{action="necklace_cast",parameter=c.necklaceCastLevel+1,description="原生项链铸造下一等级 "+(c.necklaceCastLevel+1)+"；11160×"+costs[0]+"，12240×"+costs[1]});
  }
  public static LifecycleStep36 Execute(GamePlayer p,LifecycleAction36 a,Action<GSPacketIn> receive){if(receive==null)throw new InvalidOperationException("Native packet receiver required");if(p.CurrentRoom!=null||p.CurrentSpaRoom!=null||p.CurrentMarryRoom!=null||p.IsInWorldBossRoom||Lifecycle36.ForgeBusy(p))return new LifecycleStep36{action=a.action,state="当前场景或暂存物品不允许执行成长动作"};Templates();int beforeMoney=p.PlayerCharacter.Money,beforeHonor=p.PlayerCharacter.myHonor,beforeTotem=p.PlayerCharacter.TotemId,beforeNeck=p.PlayerCharacter.necklaceCastLevel,before1=Count(p,11160),before2=Count(p,12240);GSPacketIn packet;
   if(a.action=="totem_activate"){var next=NextTotem(p);if(!AllowedTotem(p,next)||a.parameter!=next.ID)return new LifecycleStep36{action=a.action,state="图腾下一点或真实荣誉/点券已变化"};packet=new GSPacketIn(136,p.PlayerId);packet.WriteBoolean(false);packet.WriteBoolean(false);}
   else if(a.action=="necklace_cast"){int[] costs;if(!NecklaceCosts.TryGetValue(beforeNeck,out costs)||a.parameter!=beforeNeck+1||!AllowedNecklace(p,costs))return new LifecycleStep36{action=a.action,state="项链或真实铸造材料已变化"};packet=new GSPacketIn(661,p.PlayerId);}
   else throw new ArgumentException("Unknown native growth action");
   packet.Offset=GSPacketIn.HDR_SIZE;receive(packet);bool changed=beforeTotem!=p.PlayerCharacter.TotemId||beforeNeck!=p.PlayerCharacter.necklaceCastLevel||beforeMoney!=p.PlayerCharacter.Money||beforeHonor!=p.PlayerCharacter.myHonor||before1!=Count(p,11160)||before2!=Count(p,12240);return new LifecycleStep36{action=a.action,acted=changed,state="原生"+a.action+"已提交；图腾 "+beforeTotem+"→"+p.PlayerCharacter.TotemId+"，项链 "+beforeNeck+"→"+p.PlayerCharacter.necklaceCastLevel+"，荣誉 "+beforeHonor+"→"+p.PlayerCharacter.myHonor+"，点券 "+beforeMoney+"→"+p.PlayerCharacter.Money+"；材料 "+before1+"/"+before2+"→"+Count(p,11160)+"/"+Count(p,12240)};
  }
 }
}
