using System;
using System.Linq;
using System.Web.Script.Serialization;
using Local36;
public static class BotAdmin {
 public static object Roster(){return new{ok=true,settings=BotCatalog.ReadSettings(),bots=BotCatalog.ListProfiles().Select(x=>new{id=x.UserID,name=x.Name,grade=x.Level,sex=x.Sex,enabled=x.Enabled}).ToArray()};}
 public static object Create(string body){var a=new JavaScriptSerializer().Deserialize<NewBotRequest>(body);if(a==null)throw new Exception("人机信息不能为空");int id=BotCatalog.CreateBot(a);return new{ok=true,id=id,message="已加入名册，将按在线池批次上线"};}
 public static object Llm(int id){if(!BotCatalog.ListProfiles().Any(x=>x.UserID==id))throw new Exception("请选择名册中的人机");return new{ok=true,config=BotCatalog.LlmPublic(id)};}
 public static object SaveLlm(string body){var a=new JavaScriptSerializer().Deserialize<BotLlmRequest>(body);if(a==null)throw new Exception("模型配置不能为空");BotCatalog.SaveLlm(a);return new{ok=true,message="模型设置已保存，密钥不回显"};}
 public static object Lifecycle(int id){RequireBot(id);var config=LifecycleCatalog36.Read(id);return new{ok=true,config=config,state=config.state};}
 public static object SaveLifecycle(string body){var a=new JavaScriptSerializer().Deserialize<LifecycleConfig36>(body);if(a==null)throw new Exception("成长配置不能为空");RequireBot(a.userId);LifecycleCatalog36.Save(a);return new{ok=true,message=a.enabled?"自主成长已启用，将由LLM选择并执行合法动作":"自主成长已暂停"};}
 public static object Memory(int id,string conversation){RequireBot(id);return BotMemory36.Query(id,conversation??"",50);}
 public static object ClearMemory(string body){var a=new JavaScriptSerializer().Deserialize<BotMemoryClearRequest>(body);if(a==null)throw new Exception("记忆清理信息不能为空");RequireBot(a.userId);return BotMemory36.Clear(a.userId,a.conversation??"");}
 public static object MemoryDocuments(int id,string conversation){RequireBot(id);return BotMemory36.Documents(id,conversation??"");}
 public static object CorrectMemory(string body){var a=new JavaScriptSerializer().Deserialize<BotMemoryCorrectRequest>(body);if(a==null)throw new Exception("纠正信息不能为空");RequireBot(a.userId);if(String.IsNullOrWhiteSpace(a.conversation))throw new Exception("请先选择具体会话，再追加纠正");if(String.IsNullOrWhiteSpace(a.text)||a.text.Length>1000)throw new Exception("纠正内容须为1至1000字");return BotMemory36.Correct(a.userId,a.conversation,a.text);}
 public static object RollbackMemory(string body){var a=new JavaScriptSerializer().Deserialize<BotMemoryRollbackRequest>(body);if(a==null)throw new Exception("版本回滚信息不能为空");RequireBot(a.userId);if(!new[]{"persona","memories","corrections"}.Contains(a.kind)||a.versionId<=0)throw new Exception("请选择有效文档和历史版本");if(a.kind!="persona"&&String.IsNullOrWhiteSpace(a.conversation))throw new Exception("请先选择具体会话，再回滚记忆或纠正");return BotMemory36.Rollback(a.userId,a.conversation??"",a.kind,a.versionId);}
 static void RequireBot(int id){if(!BotCatalog.ListProfiles().Any(x=>x.UserID==id))throw new Exception("请选择名册中的人机");}
}
public sealed class BotMemoryClearRequest {public int userId;public string conversation;}
public sealed class BotMemoryCorrectRequest {public int userId;public string conversation;public string text;}
public sealed class BotMemoryRollbackRequest {public int userId;public string conversation;public string kind;public long versionId;}
