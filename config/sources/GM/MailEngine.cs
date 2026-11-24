using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Web.Script.Serialization;
public class GrantRequest {
 public string requestId;public int userId;public int templateId;public int count=1;public int money;public int giftToken;public int gold;public int strength;public int validDays;public bool bind=true;public string title="GM礼包";public string content="GM后台发放，请领取附件。";
}
public static class MailEngine {
 public const string Db=@"Data Source=(localdb)\DDTank36;Initial Catalog=Db_Tank_S1;Integrated Security=True";
 public static object Grant(GrantRequest a){
  Guid request;if(!Guid.TryParse(a.requestId,out request))throw new Exception("请求编号无效");
  if(a.userId<=0||a.templateId<0||a.count<1||a.count>9999||a.money<0||a.giftToken<0||a.gold<0||a.strength<0||a.strength>12||a.validDays<0||a.validDays>36500)throw new Exception("请检查数量、货币和强化等级");
  if(a.templateId==0&&a.money==0&&a.giftToken==0&&a.gold==0)throw new Exception("请选择道具或填写货币");
  if(string.IsNullOrWhiteSpace(a.title)||a.title.Length>100||a.content==null||a.content.Length>2000)throw new Exception("标题或正文长度不合适");
  string payload=new JavaScriptSerializer().Serialize(a);
  using(var db=new SqlConnection(Db)){db.Open();using(var tx=db.BeginTransaction(IsolationLevel.Serializable))try{
   var existing=Scalar(db,tx,"SELECT MailIDs FROM Local36_GM_Audit WHERE RequestID=@id",new SqlParameter("@id",request));
   if(existing!=null&&existing!=DBNull.Value){string original=Convert.ToString(Scalar(db,tx,"SELECT Payload FROM Local36_GM_Audit WHERE RequestID=@id",new SqlParameter("@id",request)));if(original!=payload)throw new Exception("同一请求编号不能用于不同礼包，请刷新后重新提交");tx.Commit();return new{ok=true,duplicate=true,mailIds=new JavaScriptSerializer().Deserialize<int[]>(Convert.ToString(existing))};}
   string nick=Convert.ToString(Scalar(db,tx,"SELECT NickName FROM Sys_User_Detail WHERE UserID=@id AND IsExist=1",new SqlParameter("@id",a.userId)));if(string.IsNullOrEmpty(nick))throw new Exception("角色不存在");
   string itemName="";int maxCount=1;bool strengthen=false;
   if(a.templateId>0){using(var c=Command(db,tx,"SELECT Name,MaxCount,CanStrengthen FROM Shop_Goods WHERE TemplateID=@id",new SqlParameter("@id",a.templateId)))using(var r=c.ExecuteReader()){if(!r.Read())throw new Exception("道具模板不存在");itemName=r.GetString(0);maxCount=Math.Max(1,r.GetInt32(1));strengthen=r.GetBoolean(2);}if(a.strength>0&&!strengthen)throw new Exception("此道具不支持强化");if((a.count+maxCount-1)/maxCount>100)throw new Exception("一次最多发放100个堆叠，请减少数量");}
   foreach(var currency in new[]{new[]{"Money",a.money.ToString()},new[]{"GiftToken",a.giftToken.ToString()},new[]{"Gold",a.gold.ToString()}}){long pending=Convert.ToInt64(Scalar(db,tx,"SELECT CONVERT(bigint,"+currency[0]+")+(SELECT COALESCE(SUM(CONVERT(bigint,"+currency[0]+")),0) FROM Sys_User_Messages WHERE ReceiverID=@id AND IsExist=1 AND IsDelR=0) FROM Sys_User_Detail WHERE UserID=@id",new SqlParameter("@id",a.userId)));if(pending+Int64.Parse(currency[1])>Int32.MaxValue)throw new Exception("角色现有及未领取货币超过上限");}
   var attachments=new List<int>();int remain=a.templateId>0?a.count:0;
   while(remain>0){int stack=Math.Min(maxCount,remain);remain-=stack;int item=Convert.ToInt32(Scalar(db,tx,@"INSERT Sys_User_Goods(UserID,BagType,TemplateID,Place,Count,IsJudge,Color,IsExist,StrengthenLevel,IsBinds,BeginDate,ValidDate,IsUsed,Skin) VALUES(0,0,@template,-1,@count,1,'',1,@strength,@bind,GETDATE(),@days,0,'');SELECT CONVERT(int,SCOPE_IDENTITY());",new SqlParameter("@template",a.templateId),new SqlParameter("@count",stack),new SqlParameter("@strength",a.strength),new SqlParameter("@bind",a.bind),new SqlParameter("@days",a.validDays)));attachments.Add(item);}
   var mails=new List<int>();int mailCount=Math.Max(1,(attachments.Count+4)/5);
   for(int index=0;index<mailCount;index++){
    using(var c=new SqlCommand("SP_Mail_Send",db,tx)){c.CommandType=CommandType.StoredProcedure;SqlCommandBuilder.DeriveParameters(c);foreach(SqlParameter p in c.Parameters){if(p.Direction==ParameterDirection.ReturnValue)continue;if(p.Direction==ParameterDirection.Output){p.Value=0;continue;}p.Value=p.SqlDbType==SqlDbType.Bit?(object)false:p.SqlDbType==SqlDbType.Int?(object)0:p.SqlDbType==SqlDbType.DateTime?(object)DateTime.Now:(object)"";}
     c.Parameters["@SenderID"].Value=0;c.Parameters["@Sender"].Value="GM后台";c.Parameters["@ReceiverID"].Value=a.userId;c.Parameters["@Receiver"].Value=nick;c.Parameters["@Title"].Value=a.title;c.Parameters["@Content"].Value=a.content;c.Parameters["@IsExist"].Value=true;c.Parameters["@Type"].Value=51;c.Parameters["@ValidDate"].Value=365;
     c.Parameters["@Money"].Value=index==0?a.money:0;c.Parameters["@GiftToken"].Value=index==0?a.giftToken:0;c.Parameters["@Gold"].Value=index==0?a.gold:0;
     for(int slot=1;slot<=5;slot++){int pos=index*5+slot-1;if(pos<attachments.Count){c.Parameters["@Annex"+slot].Value=attachments[pos].ToString();c.Parameters["@Annex"+slot+"Name"].Value=itemName;}}
     c.Parameters["@AnnexRemark"].Value=itemName;c.ExecuteNonQuery();if(Convert.ToInt32(c.Parameters["@RETURN_VALUE"].Value)!=0)throw new Exception("原生邮件过程返回失败");mails.Add(Convert.ToInt32(c.Parameters["@ID"].Value));
    }
   }
   Scalar(db,tx,"INSERT Local36_GM_Audit(RequestID,ReceiverID,Payload,MailIDs) VALUES(@id,@user,@payload,@mails)",new SqlParameter("@id",request),new SqlParameter("@user",a.userId),new SqlParameter("@payload",payload),new SqlParameter("@mails",new JavaScriptSerializer().Serialize(mails)));
   tx.Commit();return new{ok=true,duplicate=false,mailIds=mails.ToArray(),recipient=nick};
  }catch{tx.Rollback();throw;}}
 }
 public static object Query(string sql,params SqlParameter[] parameters){var rows=new List<Dictionary<string,object>>();using(var db=new SqlConnection(Db)){db.Open();using(var c=Command(db,null,sql,parameters))using(var r=c.ExecuteReader())while(r.Read()){var row=new Dictionary<string,object>();for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i);rows.Add(row);}}return rows;}
 static SqlCommand Command(SqlConnection db,SqlTransaction tx,string sql,params SqlParameter[] parameters){var c=new SqlCommand(sql,db,tx);c.CommandTimeout=60;c.Parameters.AddRange(parameters);return c;}
 static object Scalar(SqlConnection db,SqlTransaction tx,string sql,params SqlParameter[] parameters){using(var c=Command(db,tx,sql,parameters))return c.ExecuteScalar();}
}
