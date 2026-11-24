using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using Game.Base.Packets;
using Game.Server.Managers;
namespace Local36 {
 public static class BotConsortia36 {
  static readonly Dictionary<int,DateTime> Retry=new Dictionary<int,DateTime>();
  public static void Tick(IEnumerable<BotPlayer> players){var online=players.Where(p=>p.CurrentRoom==null&&p.CurrentSpaRoom==null&&p.CurrentMarryRoom==null&&p.PlayerCharacter.ConsortiaID==0).ToDictionary(p=>p.PlayerId);if(online.Count==0)return;
   var rows=new List<int[]>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var c=new SqlCommand("SELECT i.ID,i.UserID,i.InviteID FROM Sys_Consortia_Invite_Users i JOIN Local36_Bots b ON b.UserID=i.UserID WHERE i.IsExist=1 AND b.Enabled=1 ORDER BY i.ID",db))using(var r=c.ExecuteReader())while(r.Read())rows.Add(new[]{r.GetInt32(0),r.GetInt32(1),r.GetInt32(2)});}
   foreach(var row in rows){BotPlayer bot;DateTime until;if(!online.TryGetValue(row[1],out bot)||Retry.TryGetValue(row[0],out until)&&until>DateTime.UtcNow)continue;Retry[row[0]]=DateTime.UtcNow.AddMinutes(1);
    lock(bot){if(bot.CurrentRoom!=null||bot.PlayerCharacter.ConsortiaID!=0)continue;var packet=new GSPacketIn(129,bot.PlayerId);packet.WriteInt(12);packet.WriteInt(row[0]);packet.Offset=GSPacketIn.HDR_SIZE;bot.BotClient.Receive(packet);
     if(bot.PlayerCharacter.ConsortiaID>0){bot.SaveIntoDatabase();BotService.NotifySocial(bot,WorldMgr.GetPlayerById(row[2]),"consortia_joined","已通过原生公会邀请校验并加入公会"+bot.PlayerCharacter.ConsortiaName+"。","我已加入公会，一起活动吧。");Console.WriteLine("BOT CONSORTIA ACCEPTED: bot="+bot.PlayerId+" guild="+bot.PlayerCharacter.ConsortiaID+" invitation="+row[0]);}
    }
   }
  }
 }
}
