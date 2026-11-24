using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
using Game.Logic;
using Fighting.Server.Rooms;
using HarmonyLib;
namespace Local36 {
 public sealed class ExistingBotPairRequest36 {public string id;public int[] players,bots;public DateTime expiresUtc;}
 // Runs inside the native matching thread. Only pair the exact existing parties
 // selected by Road, after its real-player waiting window. Native game creation,
 // equipment, HP, damage, loading, rewards and all combat rules are retained.
 public static class ExistingBotPairFight36 {
  static string root;static Func<HashSet<int>> residents;static bool installed;
  public static void Install(string directory,Func<HashSet<int>> ids){if(installed)return;root=directory;residents=ids;var method=typeof(ProxyRoomMgr).GetMethod("PickUpRooms",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);new Harmony("ddtank36.existing.bot.pairs").Patch(method,prefix:new HarmonyMethod(typeof(ExistingBotPairFight36).GetMethod("BeforePickup",BindingFlags.NonPublic|BindingFlags.Static)));installed=true;}
  static int[] Members(ProxyRoom room){return room.GetPlayers().Select(p=>p.PlayerCharacter.ID).OrderBy(id=>id).ToArray();}
  static MatchMember36[] Team(ProxyRoom room){return room.GetPlayers().Select(p=>new MatchMember36{Id=p.PlayerCharacter.ID,Level=p.PlayerCharacter.Grade,Power=p.PlayerCharacter.FightPower}).ToArray();}
  static bool Same(int[] left,int[] right){return left!=null&&right!=null&&left.OrderBy(i=>i).SequenceEqual(right.OrderBy(i=>i));}
  static void BeforePickup(){try{string folder=Path.Combine(root,"config","match-pairs");if(!Directory.Exists(folder))return;
   foreach(string file in Directory.GetFiles(folder,"*.json")){try{var request=new JavaScriptSerializer().Deserialize<ExistingBotPairRequest36>(File.ReadAllText(file));if(request==null||request.expiresUtc<DateTime.UtcNow){File.Delete(file);continue;}var bots=residents();if(bots==null||request.bots==null||request.bots.Length<1||request.bots.Length>4||request.bots.Any(id=>!bots.Contains(id))||request.players==null||request.players.Length!=request.bots.Length||request.players.Intersect(request.bots).Any())continue;
    var waiting=ProxyRoomMgr.GetWaitMatchRoomUnsafe().Where(p=>!p.IsPlaying&&p.Game==null&&p.RoomType==eRoomType.Match).ToArray();var left=waiting.FirstOrDefault(p=>Same(Members(p),request.players));var right=waiting.FirstOrDefault(p=>Same(Members(p),request.bots));if(left==null||right==null||left==right||left.GameType!=right.GameType)continue;
    if(waiting.Any(p=>p!=left&&p!=right&&p.GameType==left.GameType&&p.GetPlayers().Any(x=>!bots.Contains(x.PlayerCharacter.ID))&&MatchPolicy36.TeamsCompatible(Team(left),Team(p))))continue;
    typeof(ProxyRoomMgr).GetMethod("StartMatchGame",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{left,right,false});
    if(left.Game!=null&&left.Game==right.Game){File.Delete(file);Console.WriteLine("EXISTING BOT MATCH STARTED: players="+String.Join(",",request.players)+" bots="+String.Join(",",request.bots));}
   }catch(IOException){}catch(Exception e){Console.WriteLine("EXISTING BOT PAIR: "+e.GetBaseException().Message);}}
  }catch(Exception e){Console.WriteLine("EXISTING BOT PAIR SCAN: "+e.GetType().Name);}}
 }
}
