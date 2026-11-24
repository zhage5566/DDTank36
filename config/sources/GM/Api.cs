using System;
using System.Web;
using System.Web.SessionState;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using System.Configuration;
using System.Security.Cryptography;
public class GMApi:IHttpHandler,IRequiresSessionState {
 public bool IsReusable{get{return false;}}
 public void ProcessRequest(HttpContext c){c.Response.ContentType="application/json; charset=utf-8";c.Response.Cache.SetCacheability(HttpCacheability.NoCache);c.Response.Cache.SetNoStore();object result=null;var json=new JavaScriptSerializer();try{
  if(!IPAddress.IsLoopback(IPAddress.Parse(c.Request.UserHostAddress)))throw new Exception("此后台只允许本机访问");string action=c.Request.QueryString["action"]??"session";
  if(action=="session"){if(c.Session["csrf"]==null)c.Session["csrf"]=Guid.NewGuid().ToString("N");result=new{ok=true,loggedIn=c.Session["admin"]!=null,csrf=c.Session["csrf"],version="3.6"};}
  else if(action=="login"){RequirePost(c,false);string password=c.Request.Form["password"]??"";string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-","");if(hash!=ConfigurationManager.AppSettings["AdminHash"])throw new Exception("管理密码不正确");c.Session["admin"]=true;c.Session["csrf"]=Guid.NewGuid().ToString("N");result=new{ok=true,csrf=c.Session["csrf"]};}
  else {if(c.Session["admin"]==null){c.Response.StatusCode=401;throw new Exception("请先登录GM后台");}
   if(action=="users"){string q=c.Request.QueryString["q"]??"";result=MailEngine.Query("SELECT TOP 200 u.UserID,u.UserName,u.NickName,u.Grade,u.Money,u.GiftToken,u.Gold,CASE WHEN b.UserID IS NULL THEN 0 ELSE 1 END IsBot FROM Sys_User_Detail u LEFT JOIN Local36_Bots b ON b.UserID=u.UserID WHERE u.IsExist=1 AND (u.NickName LIKE @q OR u.UserName LIKE @q OR CONVERT(nvarchar(20),u.UserID)=@id) ORDER BY u.UserID",new SqlParameter("@q","%"+q+"%"),new SqlParameter("@id",q));}
   else if(action=="items"){string q=c.Request.QueryString["q"]??"";result=MailEngine.Query("SELECT TOP 150 TemplateID,Name,CategoryID,MaxCount,NeedLevel,CanStrengthen FROM Shop_Goods WHERE Name LIKE @q OR CONVERT(nvarchar(20),TemplateID)=@id ORDER BY TemplateID",new SqlParameter("@q","%"+q+"%"),new SqlParameter("@id",q));}
   else if(action=="send"){RequirePost(c,true);var request=json.Deserialize<GrantRequest>(new StreamReader(c.Request.InputStream).ReadToEnd());result=MailEngine.Grant(request);try{Runtime("/notify-mail?id="+request.userId);}catch{} }
   else if(action=="audit")result=MailEngine.Query("SELECT TOP 100 RequestID,ReceiverID,Payload,MailIDs,CreatedAt FROM Local36_GM_Audit ORDER BY CreatedAt DESC");
   else if(action=="bots")result=json.DeserializeObject(Runtime("/status"));
   else if(action=="bot-roster")result=BotAdmin.Roster();
   else if(action=="bot-create"){RequirePost(c,true);result=BotAdmin.Create(new StreamReader(c.Request.InputStream).ReadToEnd());try{Runtime("/reload",true);}catch{}}
   else if(action=="bot-llm"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=BotAdmin.Llm(id);}
   else if(action=="bot-llm-save"){RequirePost(c,true);result=BotAdmin.SaveLlm(new StreamReader(c.Request.InputStream).ReadToEnd());try{Runtime("/reload",true);}catch{}}
   else if(action=="bot-llm-test"){RequirePost(c,true);int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=json.DeserializeObject(Runtime("/gm/llm-test?id="+id,true));}
    else if(action=="bot-lifecycle"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=BotAdmin.Lifecycle(id);}
    else if(action=="bot-lifecycle-save"){RequirePost(c,true);string body=new StreamReader(c.Request.InputStream).ReadToEnd();var config=json.Deserialize<Local36.LifecycleConfig36>(body);result=BotAdmin.SaveLifecycle(body);Runtime("/lifecycle/invalidate?id="+config.userId,true);try{Runtime("/reload",true);}catch{}}
   else if(action=="bot-progression"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=json.DeserializeObject(Runtime("/gm/progression?id="+id));}
   else if(action=="bot-progression-save"){RequirePost(c,true);if(c.Request.ContentLength>32768)throw new Exception("修改内容过长");result=json.DeserializeObject(Runtime("/gm/progression-save",true,new StreamReader(c.Request.InputStream).ReadToEnd()));}
   else if(action=="bot-progression-audit"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=Local36.ProgressionAudit36.Recent(id);}
   else if(action=="bot-native-growth"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=json.DeserializeObject(Runtime("/gm/native-growth?id="+id));}
   else if(action=="bot-native-growth-step"){RequirePost(c,true);if(c.Request.ContentLength>8192)throw new Exception("操作内容过长");result=json.DeserializeObject(Runtime("/gm/native-growth-step",true,new StreamReader(c.Request.InputStream).ReadToEnd()));}
   else if(action=="bot-memory"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=BotAdmin.Memory(id,c.Request.QueryString["conversation"]);}
   else if(action=="bot-memory-clear"){RequirePost(c,true);result=BotAdmin.ClearMemory(new StreamReader(c.Request.InputStream).ReadToEnd());}
   else if(action=="bot-memory-docs"){int id;if(!Int32.TryParse(c.Request.QueryString["id"],out id))throw new Exception("角色ID无效");result=BotAdmin.MemoryDocuments(id,c.Request.QueryString["conversation"]);}
   else if(action=="bot-memory-correct"){RequirePost(c,true);result=BotAdmin.CorrectMemory(new StreamReader(c.Request.InputStream).ReadToEnd());}
   else if(action=="bot-memory-rollback"){RequirePost(c,true);result=BotAdmin.RollbackMemory(new StreamReader(c.Request.InputStream).ReadToEnd());}
   else if(action=="logout"){RequirePost(c,true);c.Session.Clear();result=new{ok=true};}
   else throw new Exception("未知操作");
  }
 }catch(Exception e){if(c.Response.StatusCode==200)c.Response.StatusCode=400;result=new{ok=false,error=e.Message};}c.Response.Write(json.Serialize(result));}
 static void RequirePost(HttpContext c,bool csrf){if(c.Request.HttpMethod!="POST")throw new Exception("必须通过POST提交");if(csrf&&(string)c.Session["csrf"]!=c.Request.Headers["X-CSRF-Token"])throw new Exception("页面会话已过期，请刷新");}
 static string Runtime(string path,bool post=false,string body=null){var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:9440"+path);request.Proxy=null;request.Timeout=15000;request.Headers["X-DDT-Token"]=File.ReadAllText(Path.Combine(Local36.Paths36.Root,"config","runtime-token.txt")).Trim();if(post){request.Method="POST";byte[] bytes=body==null?new byte[0]:Encoding.UTF8.GetBytes(body);request.ContentType="application/json; charset=utf-8";request.ContentLength=bytes.Length;if(bytes.Length>0)using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}try{using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return reader.ReadToEnd();}catch(WebException e){var response=e.Response;if(response==null)throw;using(response)using(var reader=new StreamReader(response.GetResponseStream()))throw new Exception(reader.ReadToEnd());}}
}
