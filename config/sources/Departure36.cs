using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Game.Server.GameObjects;
namespace Local36 {
 public static class Departure36 {
  sealed class Friend {public GamePlayer Player;public int Sent;}
  sealed class Leaving {public GamePlayer Bot;public Friend[] Friends;public DateTime Deadline;}
  static readonly ConcurrentDictionary<int,Leaving> Pending=new ConcurrentDictionary<int,Leaving>();
  static readonly object Gate=new object();
  public static bool Ready(GamePlayer bot,IEnumerable<GamePlayer> realPlayers,Func<bool> stillLeaving,Action<GamePlayer,GamePlayer,string> deliver,bool maintenance=false){
   Leaving state;bool created=false;lock(Gate){if(!Pending.TryGetValue(bot.PlayerId,out state)||!Object.ReferenceEquals(state.Bot,bot)){
    var friends=new HashSet<int>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var c=new SqlCommand("SELECT FriendID FROM Sys_User_Friends WHERE UserID=@id AND IsExist=1 AND Relation=0",db)){c.Parameters.AddWithValue("@id",bot.PlayerId);using(var r=c.ExecuteReader())while(r.Read())friends.Add(r.GetInt32(0));}}
    state=new Leaving{Bot=bot,Deadline=DateTime.UtcNow.AddSeconds(18),Friends=realPlayers.Where(p=>friends.Contains(p.PlayerId)&&p.IsActive).Select(p=>new Friend{Player=p}).ToArray()};Pending[bot.PlayerId]=state;created=true;
   }}
   if(created){
    foreach(var entry in state.Friends){var friend=entry;Action<string> reply=text=>{if(Interlocked.Exchange(ref friend.Sent,1)!=0)return;if(stillLeaving()&&friend.Player.IsActive)try{deliver(bot,friend.Player,text);}catch(Exception e){Console.WriteLine("BOT OFFLINE NOTICE: "+e.GetType().Name);}};
     string fallback=maintenance?"服务器要维护了，我先下线，之后再一起玩。":"我先下线啦，下次再一起打副本。";
     string prompt="下线事件：你即将"+(maintenance?"因服务器维护":"因在线批次轮换")+"下线。玩家"+friend.Player.PlayerCharacter.NickName+"是你的在线好友。请依据角色性格和已有交往记忆，主动发一句简短告别，不承诺准确的上线时间。";
     if(!Llm36.Submit(bot.PlayerId,"private_offline",prompt,"player/"+friend.Player.PlayerId,reply,()=>reply(fallback)))reply(fallback);
    }
    Console.WriteLine("BOT DEPARTURE EVENT: "+bot.PlayerId+" onlineFriends="+state.Friends.Length);
   }
   if(DateTime.UtcNow>=state.Deadline)foreach(var friend in state.Friends)if(Interlocked.Exchange(ref friend.Sent,1)==0&&stillLeaving()&&friend.Player.IsActive)try{deliver(bot,friend.Player,"我先下线啦，下次再一起玩。");}catch{}
   return state.Friends.All(f=>Interlocked.CompareExchange(ref f.Sent,0,0)!=0);
  }
  public static void Forget(int id){lock(Gate){Leaving state;Pending.TryRemove(id,out state);}}
  public static void Reset(){lock(Gate)Pending.Clear();}
 }
}
