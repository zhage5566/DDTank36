using System;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using Bussiness;
namespace Local36 {
 public sealed class GMNativeGrowthRequest36 {public int userId;public string requestId;public LifecycleAction36 selection;}
 public static partial class BotService {
  static object NativeGrowthAdmin(HttpListenerContext ctx,bool save){
   var json=new JavaScriptSerializer();GMNativeGrowthRequest36 request=null;int id;
   if(save){if(ctx.Request.HttpMethod!="POST"||ctx.Request.ContentLength64>8192)throw new ArgumentException("Invalid native growth request");string raw;using(var reader=new StreamReader(ctx.Request.InputStream))raw=reader.ReadToEnd();if(raw.Length>8192)throw new ArgumentException("Request too large");request=json.Deserialize<GMNativeGrowthRequest36>(raw);if(request==null||request.selection==null)throw new ArgumentException("请选择原生系统操作");id=request.userId;}else id=Int32.Parse(ctx.Request.QueryString["id"]??"0");
   if(!Roster.ContainsKey(id))throw new ArgumentException("请选择名册中的人机");
   lock(BotAdminGate){BotPlayer player;bool temporary=!Bots.TryGetValue(id,out player);if(temporary){using(var db=new PlayerBussiness()){var info=db.GetUserSingleByUserID(id);if(info==null)throw new InvalidOperationException("角色不存在");player=new BotPlayer(info,new BotClient());if(!player.LoadFromDatabase())throw new InvalidOperationException("原生加载失败");player.Online=false;player.PlayerCharacter.State=0;InitializeCombatProfile(player);}}
    try{lock(player){if(player.CurrentRoom!=null||player.CurrentSpaRoom!=null||player.CurrentMarryRoom!=null||player.IsInWorldBossRoom)throw new InvalidOperationException("请等待角色退出房间和活动场景");
     if(!save)return new{ok=true,online=!temporary,profile=Lifecycle36.Inspect(player)};
     Guid key;if(!Guid.TryParse(request.requestId,out key)||key==Guid.Empty)throw new ArgumentException("操作编号无效");request.requestId=key.ToString();
     string payload=json.Serialize(CanonicalJsonValue(json.DeserializeObject(json.Serialize(request))));string duplicate=ProgressionAudit36.Begin(key,id,payload,json.Serialize(Lifecycle36.Inspect(player)));if(duplicate!=null)return json.DeserializeObject(duplicate);
     try{var step=Lifecycle36.Execute(player,request.selection,12,player.BotClient.Receive);if(!player.SaveIntoDatabase())throw new InvalidOperationException("原生保存失败");var after=Lifecycle36.Inspect(player);string result=json.Serialize(new{ok=true,step=step,profile=after});ProgressionAudit36.Complete(key,result,null);BotLifecycleDriver36.Invalidate(id);RankSync36.MarkDirty();return json.DeserializeObject(result);}catch(Exception e){ProgressionAudit36.Complete(key,null,e.GetBaseException().Message);throw;}
    }}finally{if(temporary){player.Online=false;player.PlayerCharacter.State=0;if(!player.SaveIntoDatabase())throw new InvalidOperationException("离线角色保存失败");}}
   }
  }
 }
}
