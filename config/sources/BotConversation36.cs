using System;
using System.Collections.Generic;
using System.Linq;
using Game.Server.GameObjects;
namespace Local36 {
 public static class BotConversation36 {
  static readonly Dictionary<int,DateTime> LastPrivate=new Dictionary<int,DateTime>();
  static DateTime Next=DateTime.UtcNow.AddMinutes(2),Hall=DateTime.UtcNow.AddMinutes(3);
  public static void Tick(IEnumerable<BotPlayer> roster,IEnumerable<GamePlayer> players){
   if(DateTime.UtcNow<Next)return;Next=DateTime.UtcNow.AddSeconds(60);
   var bots=roster.Where(p=>SceneInteraction36.IsIdle(p)&&!Lifecycle36.ForgeBusy(p)).ToArray();
   var humans=players.Where(p=>!BotService.IsBot(p.PlayerId)&&!p.IsRobot&&SceneInteraction36.IsIdle(p)).ToArray();
   foreach(var human in humans){DateTime last;if(LastPrivate.TryGetValue(human.PlayerId,out last)&&(DateTime.UtcNow-last).TotalMinutes<10)continue;
    var friend=bots.Where(b=>b.AreaID==human.AreaID&&b.Friends!=null&&b.Friends.ContainsKey(human.PlayerId)).OrderBy(b=>Math.Abs(b.PlayerCharacter.Grade-human.PlayerCharacter.Grade)).ThenBy(b=>b.PlayerId).FirstOrDefault();
    if(friend==null)continue;LastPrivate[human.PlayerId]=DateTime.UtcNow;
    BotService.NotifySocial(friend,human,"proactive_greeting","双方都是在线好友，目前在大厅休息。结合已有记忆主动问候，不声称已经发送组队邀请。","在大厅遇到你啦，想一起做任务的话可以邀请我。");break;
   }
   if(DateTime.UtcNow<Hall||humans.Length==0||bots.Length==0)return;Hall=DateTime.UtcNow.AddMinutes(3);
   var speaker=bots[(int)((DateTime.UtcNow.Ticks/TimeSpan.TicksPerMinute)%bots.Length)];Action<string> send=text=>{if(SceneInteraction36.IsIdle(speaker))BotService.Speak(speaker,text,null,5);};
   if(!Llm36.Submit(speaker.PlayerId,"proactive_hall","你现在在游戏大厅休息。结合性格和已有记忆发一句简短自然的大厅消息，可以询问是否有人组队，不冒充玩家、不宣称完成了未完成的任务。","hall/"+speaker.AreaID,send,()=>send("副本缺人可以邀请我，一起做任务吧。")))send("副本缺人可以邀请我，一起做任务吧。");
  }
 }
}
