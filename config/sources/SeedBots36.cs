using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
class SeedBots36 {
 const string Connection=@"Data Source=(localdb)\DDTank36;Initial Catalog=Db_Tank_S1;Integrated Security=True";
 static Random random=new Random(360014);
 static SqlConnection db;
 static object Scalar(string sql,params object[] values){using(var c=new SqlCommand(sql,db)){for(int i=0;i<values.Length;i++)c.Parameters.AddWithValue("@p"+i,values[i]);return c.ExecuteScalar();}}
 static List<Dictionary<string,object>> Rows(string sql){var list=new List<Dictionary<string,object>>();using(var c=new SqlCommand(sql,db))using(var r=c.ExecuteReader()){while(r.Read()){var d=new Dictionary<string,object>();for(int i=0;i<r.FieldCount;i++)d[r.GetName(i)]=r.GetValue(i);list.Add(d);}}return list;}
 static int Pick(List<Dictionary<string,object>> pool,int category,int level,bool male){var p=pool.Where(x=>Convert.ToInt32(x["CategoryID"])==category&&Convert.ToInt32(x["NeedLevel"])<=level&&(Convert.ToInt32(x["NeedSex"])==0||Convert.ToInt32(x["NeedSex"])==(male?1:2))).ToArray();if(p.Length==0)throw new Exception("Missing equipment category "+category);return Convert.ToInt32(p[random.Next(p.Length)]["TemplateID"]);}
 static void Main(){try{Run();}catch(Exception e){Console.WriteLine(e.GetType().FullName+": "+e.Message);Console.WriteLine(e.StackTrace);Environment.ExitCode=1;}}
 static void Run(){db=new SqlConnection(Connection);db.Open();
 Scalar(@"IF OBJECT_ID('dbo.Local36_Bots') IS NULL CREATE TABLE dbo.Local36_Bots(UserID int NOT NULL PRIMARY KEY, Level int NOT NULL, Stage nvarchar(30) NOT NULL, Enabled bit NOT NULL DEFAULT 1, Profile nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL DEFAULT sysdatetime());
 IF OBJECT_ID('dbo.Local36_GM_Audit') IS NULL CREATE TABLE dbo.Local36_GM_Audit(RequestID uniqueidentifier PRIMARY KEY,ReceiverID int NOT NULL, Payload nvarchar(max) NOT NULL, MailIDs nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL DEFAULT sysdatetime());");
 var pool=Rows("SELECT TemplateID,CategoryID,NeedSex,NeedLevel FROM Shop_Goods WHERE CanEquip=1 AND Valid=1 AND CategoryID IN (1,2,3,4,5,6,13,15)");
 int[] levels={1,5,10,15,20,25,30,35,40,45,50,60};
 string[] names={"清风","星河","暖阳","青柠","流萤","云帆","白露","月影","竹影","晨曦","小满","落樱"};
 var profiles=new List<Dictionary<string,object>>();
 for(int tier=0;tier<levels.Length;tier++)for(int n=0;n<4;n++){
  int level=levels[tier];bool male=n%2==0;string username="bot36_"+level+"_"+n;string nick="人机"+names[tier]+(n+1).ToString("00");
  int id=Convert.ToInt32(Scalar("SELECT ISNULL(MAX(UserID),0) FROM Sys_User_Detail WHERE UserName=@p0",username));
  int gp=Convert.ToInt32(Scalar("SELECT GP FROM Level WHERE Grade=@p0",level));
  string stage=level<10?"新手":level<25?"成长":level<40?"进阶":"高阶";
  int strength=level<10?0:level<20?3:level<30?6:level<40?8:level<50?10:12;
  int[] weapons=level<10?new[]{7001,7002,7003}:level<25?new[]{7006,7007,7008,7009}:level<40?new[]{7015,7016,7017,7018}:new[]{7024,7026,7027,7032,7045,7046};
  int weapon=weapons[random.Next(weapons.Length)];
  var equip=new Dictionary<int,int>();equip[0]=Pick(pool,1,level,male);equip[1]=Pick(pool,2,level,male);equip[2]=Pick(pool,3,level,male);equip[3]=Pick(pool,4,level,male);equip[4]=Pick(pool,5,level,male);equip[5]=Pick(pool,6,level,male);equip[6]=weapon;
  if(level>=20){equip[11]=Pick(pool,13,level,male);equip[13]=Pick(pool,15,level,male);}
  var profile=new Dictionary<string,object>{{"Username",username},{"Name",nick},{"Level",level},{"Sex",male},{"Stage",stage},{"Strength",strength},{"Equips",equip.ToDictionary(x=>x.Key.ToString(),x=>x.Value)}};
  if(id==0 || Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Local36_Bots WHERE UserID=@p0",id))==0){
   if(id==0){
   using(var c=new SqlCommand("SP_Users_Active",db)){c.CommandType=CommandType.StoredProcedure;SqlCommandBuilder.DeriveParameters(c);foreach(SqlParameter p in c.Parameters){if(p.Direction==ParameterDirection.ReturnValue)continue;if(p.Direction==ParameterDirection.Output){p.Value=0;continue;}p.Value=p.SqlDbType==SqlDbType.Bit?(object)false:p.SqlDbType==SqlDbType.Int?(object)0:(object)"";}
    foreach(string key in new[]{"Attack","Defence","Luck","Agility"})c.Parameters["@"+key].Value=10+level*2;
    c.Parameters["@UserName"].Value=username;c.Parameters["@PassWord"].Value=Guid.NewGuid().ToString("N");c.Parameters["@Sex"].Value=male;c.Parameters["@Grade"].Value=level;c.Parameters["@GP"].Value=gp;c.Parameters["@State"].Value=1;c.Parameters["@Gold"].Value=1000000;c.Parameters["@Money"].Value=100000;c.Parameters["@GiftToken"].Value=100000;c.Parameters["@ActiveIP"].Value="127.0.0.1";c.ExecuteNonQuery();if(Convert.ToInt32(c.Parameters["@RETURN_VALUE"].Value)!=0)throw new Exception("Bot activation failed");id=Convert.ToInt32(c.Parameters["@UserID"].Value);
   }}
   Scalar("UPDATE Sys_User_Detail SET NickName=@p0,Grade=@p1,GP=@p2,IsFirst=4,State=1,Style=@p3,Colors=',,,,,,',Skin='',IsExist=1 WHERE UserID=@p4",nick,level,gp,string.Join(",",new[]{equip[0],equip[1],equip[2],equip[3],equip[4],equip[5],equip[6]})+",,,,,,,,,,",id);
   Scalar("DELETE FROM Sys_User_Goods WHERE UserID=@p0",id);
   foreach(var e in equip){int cat=e.Key==6?7:e.Key==4?5:0;int s=cat>0?strength:0;
    Scalar("INSERT Sys_User_Goods(UserID,BagType,TemplateID,Place,Count,IsJudge,Color,IsExist,StrengthenLevel,AttackCompose,DefendCompose,LuckCompose,AgilityCompose,IsBinds,BeginDate,ValidDate,IsUsed,Skin) VALUES(@p0,0,@p1,@p2,1,1,'',1,@p3,@p4,@p4,@p4,@p4,1,GETDATE(),0,1,'')",id,e.Value,e.Key,s,level<10?0:Math.Min(50,level));
   }
   profile["UserID"]=id;string json=new JavaScriptSerializer().Serialize(profile);
   Scalar("INSERT Local36_Bots(UserID,Level,Stage,Profile) VALUES(@p0,@p1,@p2,@p3)",id,level,stage,json);
  }else{profile=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>((string)Scalar("SELECT Profile FROM Local36_Bots WHERE UserID=@p0",id));}
  profiles.Add(profile);Console.WriteLine("Bot "+id+" "+nick+" level="+level+" "+stage);
 }
 string root=@"D:\DDTank-3.6-Frank-CN\config";File.WriteAllText(Path.Combine(root,"bots36.json"),new JavaScriptSerializer().Serialize(profiles));db.Close();
 }
}

