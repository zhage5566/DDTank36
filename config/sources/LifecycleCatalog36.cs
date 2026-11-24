using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
namespace Local36 {
 public class LifecycleConfig36 {
  public int userId;public bool enabled;public bool autoQuest=true,autoStrengthen=true,invitePlayers=true;
  public int maxStrengthen=6,invitationInterval=180;public string state="未启用自主成长",lastTickUtc;
 }
 public sealed class LifecyclePlan36 {public string configKey;public string[] priorities;public int actionsLeft;public DateTime untilUtc;}
 public static class LifecycleCatalog36 {
  static readonly object Gate=new object();static volatile bool ready;
  public static void EnsureSchema(){if(ready)return;lock(Gate){if(ready)return;using(var db=Open())using(var c=new SqlCommand(@"SET XACT_ABORT ON; BEGIN TRAN; DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=N'Local36_LifecycleSchema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @lock<0 THROW 50000,'Lifecycle schema busy',1;
IF OBJECT_ID('dbo.Local36_BotLifecycle') IS NULL CREATE TABLE dbo.Local36_BotLifecycle(UserID int PRIMARY KEY,Enabled bit NOT NULL,AutoQuest bit NOT NULL,AutoStrengthen bit NOT NULL,InvitePlayers bit NOT NULL,MaxStrengthen int NOT NULL,InvitationInterval int NOT NULL,State nvarchar(1000) NOT NULL DEFAULT N'',LastTickUtc datetime2 NULL,UpdatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.Local36_LifecyclePlan') IS NULL CREATE TABLE dbo.Local36_LifecyclePlan(UserID int PRIMARY KEY,PlanJson nvarchar(4000) NOT NULL,UpdatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()); COMMIT;",db))c.ExecuteNonQuery();ready=true;}}
  public static LifecyclePlan36 ReadPlan(int id){EnsureSchema();using(var db=Open())using(var c=new SqlCommand("SELECT PlanJson FROM dbo.Local36_LifecyclePlan WHERE UserID=@id",db)){c.Parameters.AddWithValue("@id",id);var raw=c.ExecuteScalar() as string;if(raw==null)return null;try{return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<LifecyclePlan36>(raw);}catch{return null;}}}
  public static void SavePlan(int id,LifecyclePlan36 plan){EnsureSchema();using(var db=Open())using(var tx=db.BeginTransaction(IsolationLevel.Serializable))using(var c=new SqlCommand("UPDATE dbo.Local36_LifecyclePlan WITH(UPDLOCK,HOLDLOCK) SET PlanJson=@json,UpdatedUtc=SYSUTCDATETIME() WHERE UserID=@id;IF @@ROWCOUNT=0 INSERT dbo.Local36_LifecyclePlan(UserID,PlanJson) VALUES(@id,@json)",db,tx)){c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@json",new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(plan));c.ExecuteNonQuery();tx.Commit();}}
  public static void ClearPlan(int id){EnsureSchema();using(var db=Open())using(var c=new SqlCommand("DELETE dbo.Local36_LifecyclePlan WHERE UserID=@id",db)){c.Parameters.AddWithValue("@id",id);c.ExecuteNonQuery();}}
  public static LifecycleConfig36 Read(int id){EnsureSchema();using(var db=Open())using(var c=new SqlCommand("SELECT UserID,Enabled,AutoQuest,AutoStrengthen,InvitePlayers,MaxStrengthen,InvitationInterval,State,LastTickUtc FROM dbo.Local36_BotLifecycle WHERE UserID=@id",db)){c.Parameters.AddWithValue("@id",id);using(var r=c.ExecuteReader())return r.Read()?Row(r):new LifecycleConfig36{userId=id};}}
  public static List<LifecycleConfig36> Enabled(){EnsureSchema();var rows=new List<LifecycleConfig36>();using(var db=Open())using(var c=new SqlCommand("SELECT UserID,Enabled,AutoQuest,AutoStrengthen,InvitePlayers,MaxStrengthen,InvitationInterval,State,LastTickUtc FROM dbo.Local36_BotLifecycle WHERE Enabled=1",db))using(var r=c.ExecuteReader())while(r.Read())rows.Add(Row(r));return rows;}
  static LifecycleConfig36 Row(SqlDataReader r){return new LifecycleConfig36{userId=r.GetInt32(0),enabled=r.GetBoolean(1),autoQuest=r.GetBoolean(2),autoStrengthen=r.GetBoolean(3),invitePlayers=r.GetBoolean(4),maxStrengthen=r.GetInt32(5),invitationInterval=r.GetInt32(6),state=r.GetString(7),lastTickUtc=r.IsDBNull(8)?null:DateTime.SpecifyKind(r.GetDateTime(8),DateTimeKind.Utc).ToString("o")};}
  public static void Save(LifecycleConfig36 a){
   if(a==null||a.userId<=0)throw new ArgumentException("请选择人机");if(a.maxStrengthen<0||a.maxStrengthen>12||a.invitationInterval<60||a.invitationInterval>3600)throw new ArgumentException("强化目标0至12，邀请间隔60至3600秒");
   if(a.enabled){var model=BotCatalog.ReadLlm(a.userId);if(model==null||!model.Enabled||String.IsNullOrWhiteSpace(model.ApiKey)||String.IsNullOrWhiteSpace(model.Model))throw new ArgumentException("自主成长依赖LLM，请先启用该人机的模型并配置密钥");}
   EnsureSchema();using(var db=Open())using(var tx=db.BeginTransaction(IsolationLevel.Serializable))try{
    using(var exists=new SqlCommand("SELECT COUNT(*) FROM dbo.Local36_Bots WHERE UserID=@id",db,tx)){exists.Parameters.AddWithValue("@id",a.userId);if(Convert.ToInt32(exists.ExecuteScalar())!=1)throw new ArgumentException("角色不在人机名册中");}
    using(var c=new SqlCommand(@"UPDATE dbo.Local36_BotLifecycle SET Enabled=@enabled,AutoQuest=@quest,AutoStrengthen=@strength,InvitePlayers=@invite,MaxStrengthen=@max,InvitationInterval=@interval,State=@state,UpdatedUtc=SYSUTCDATETIME() WHERE UserID=@id;IF @@ROWCOUNT=0 INSERT dbo.Local36_BotLifecycle(UserID,Enabled,AutoQuest,AutoStrengthen,InvitePlayers,MaxStrengthen,InvitationInterval,State) VALUES(@id,@enabled,@quest,@strength,@invite,@max,@interval,@state);",db,tx)){
     c.Parameters.AddWithValue("@id",a.userId);c.Parameters.AddWithValue("@enabled",a.enabled);c.Parameters.AddWithValue("@quest",a.autoQuest);c.Parameters.AddWithValue("@strength",a.autoStrengthen);c.Parameters.AddWithValue("@invite",a.invitePlayers);c.Parameters.AddWithValue("@max",a.maxStrengthen);c.Parameters.AddWithValue("@interval",a.invitationInterval);c.Parameters.AddWithValue("@state",a.enabled?"已启用，等待上线并由LLM决策":"已暂停自主成长");c.ExecuteNonQuery();}tx.Commit();
   }catch{tx.Rollback();throw;}
   ClearPlan(a.userId);
  }
  public static void SetState(int id,string state){EnsureSchema();state=state??"";if(state.Length>1000)state=state.Substring(0,1000);using(var db=Open())using(var c=new SqlCommand("UPDATE dbo.Local36_BotLifecycle SET State=@state,LastTickUtc=SYSUTCDATETIME() WHERE UserID=@id AND (State<>@state OR LastTickUtc IS NULL OR LastTickUtc<DATEADD(minute,-1,SYSUTCDATETIME()))",db)){c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@state",state);c.ExecuteNonQuery();}}
  static SqlConnection Open(){var c=new SqlConnection(Paths36.Db);c.Open();return c;}
 }
}
