using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using HarmonyLib;
using Game.Logic;
using Game.Logic.Phy.Object;
using Game.Logic.Actions;
using Local36;
class LocalFightHost {
 static bool GuardMatch(object __0,object __1){try{
  RefreshProfiles();var left=RoomMembers(__0);var right=RoomMembers(__1);
  if(IsLegacyNpcRoom(__0)||IsLegacyNpcRoom(__1)){Console.WriteLine("BOT MATCH BLOCKED: legacy NPC room; resident opponents required");return false;}
  if(!left.Concat(right).Any(x=>bots.Contains(x.Id)))return true;
  bool allowed=left.Length>=1&&left.Length<=4&&left.Length==right.Length;if(!allowed)Console.WriteLine("BOT MATCH BLOCKED: "+string.Join(",",left.Select(m=>m.Id+"@"+m.Level+"/"+m.Power))+" vs "+string.Join(",",right.Select(m=>m.Id+"@"+m.Level+"/"+m.Power)));return allowed;
 }catch(Exception e){Console.WriteLine("BOT MATCH GUARD: "+e.GetType().Name);return false;}}
 static bool IsLegacyNpcRoom(object room){var property=room.GetType().GetProperty("isAutoBot");return property!=null&&(bool)property.GetValue(room,null);}
 static MatchMember36[] RoomMembers(object room){var players=(System.Collections.IEnumerable)room.GetType().GetMethod("GetPlayers").Invoke(room,null);var result=new List<MatchMember36>();foreach(IGamePlayer player in players){var p=player.PlayerCharacter;result.Add(new MatchMember36{Id=p.ID,Level=p.Grade,Power=p.FightPower});}return result.ToArray();}
 // The resident pool supplies real, fully equipped opponents of equal party size.
 // Suppress the package's unrelated one-NPC fallback after forty seconds.
 static bool ResidentOpponentsOnly(){return false;}
 static HashSet<int> bots;
 static HashSet<string> names;
 static Dictionary<string,int> turns=new Dictionary<string,int>();
 static Dictionary<string,bool> ended=new Dictionary<string,bool>();static string root;static DateTime lastProfiles=DateTime.MinValue;
 static void RefreshProfiles(){ExistingBotPairFight36.Install(root,()=>bots);NativeBotTurn36.IsResidentBot=id=>bots!=null&&bots.Contains(id);NativeRewards36.Install(id=>bots!=null&&bots.Contains(id));lock(turns){if((DateTime.UtcNow-lastProfiles).TotalSeconds<5)return;lastProfiles=DateTime.UtcNow;try{var data=new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(root,"config","bots36.json")));bots=new HashSet<int>(data.Select(x=>Convert.ToInt32(x["UserID"])));names=new HashSet<string>(data.Select(x=>Convert.ToString(x["Name"])));}catch{if(bots==null)bots=new HashSet<int>();}}}
 static IEnumerable<Living> EnemyLivings(BaseGame game){var npcs=game.FindAllNpcLiving().Cast<Living>();var pve=game as PVEGame;return pve==null?npcs:npcs.Concat(pve.FindAllBoss());}
 static void BeforeTurnEnds(BaseGame __instance){try{
  RefreshProfiles();if(__instance.GameState!=eGameState.Playing)return;foreach(var actor in __instance.GetAllPlayers()){if(actor.PlayerDetail==null)continue;var info=actor.PlayerDetail.PlayerCharacter;if(!bots.Contains(info.ID))continue;
  var pve=__instance as PVEGame;string game=System.Diagnostics.Process.GetCurrentProcess().Id+"/"+__instance.Id+"/"+(pve==null?0:pve.SessionId);int turn=__instance.TurnIndex;string key=game+"/"+info.ID+"/"+turn;lock(ended){if(ended.ContainsKey(key))continue;ended[key]=true;}
  string summary=new JavaScriptSerializer().Serialize(new{mode=__instance is PVPGame?"PVP":"PVE",self=info.NickName,turn,actor=__instance.CurrentLiving==null?"":__instance.CurrentLiving.Name,players=__instance.GetAllLivingPlayers().Select(p=>new{name=p.PlayerDetail.PlayerCharacter.NickName,hp=p.Blood,maxHp=p.MaxBlood,team=p.Team}).ToArray(),monsters=EnemyLivings(__instance).Where(p=>p.IsLiving).Take(8).Select(p=>new{name=p.Name,hp=p.Blood,maxHp=p.MaxBlood}).ToArray()});
  if(File.Exists(Path.Combine(root,"config","testing.flag")))Console.WriteLine("BOT QA TURN: "+summary);
  ThreadPool.QueueUserWorkItem(_=>{try{byte[] body=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{id=info.ID,game,turn,summary="回合结束："+summary}));var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:9440/llm/battle");request.Proxy=null;request.Method="POST";request.ContentType="application/json";request.Timeout=3000;request.Headers["X-DDT-Token"]=File.ReadAllText(Path.Combine(root,"config","runtime-token.txt")).Trim();request.ContentLength=body.Length;using(var stream=request.GetRequestStream())stream.Write(body,0,body.Length);using(var response=request.GetResponse()){}Console.WriteLine("BOT TURN EVENT: "+key);}catch(Exception e){Console.WriteLine("BOT TURN EVENT failed: "+e.GetType().Name);}});
 }}catch(Exception e){Console.WriteLine("BOT TURN SNAPSHOT: "+e.GetType().Name);}}
 static void AfterStartAttacking(Player __instance){try{
  var actor=__instance;if(actor.PlayerDetail==null||actor.PlayerDetail.PlayerCharacter==null)return;
  NativeRewards36.Observe(actor.Game);
  TakeDamageFix36.ProbeDander(actor);
  RefreshProfiles();
  var info=actor.PlayerDetail.PlayerCharacter;if(!bots.Contains(info.ID)&&!names.Contains(info.NickName))return;
  Console.WriteLine("BOT TURN: id="+info.ID+" name="+info.NickName+" alive="+actor.IsLiving+" attacking="+actor.IsAttacking);
  if(!actor.IsLiving||!actor.IsAttacking)return;
  BaseGame game=actor.Game;var pveGame=game as PVEGame;string key=game.Id+"/"+(pveGame==null?0:pveGame.SessionId)+"/"+actor.PlayerDetail.PlayerCharacter.ID;
  Living target=null;
  if(game is PVPGame)target=game.GetAllLivingPlayers().Where(p=>p.Team!=actor.Team&&p.IsLiving).OrderBy(p=>Math.Abs(p.X-actor.X)).FirstOrDefault();
  else if(game is PVEGame)target=EnemyLivings(game).Where(n=>n.IsLiving&&n.Team!=actor.Team).OrderBy(n=>Math.Abs(n.X-actor.X)).FirstOrDefault();
  NativeBotTurn36.Queue(game,actor,target);
 }catch(Exception e){Console.WriteLine("BOT AI ERROR "+e);}}
  // 敏捷→体力 现在实际作用于战斗服: Player.Reset/PrepareNewTurn 每回合都按
 // 敏捷/10+240 重算体力, 覆盖原生的 敏捷/30+240 (3.6 原实现与 3.4 一致)。
 static void CombatEnergyFix(Player __instance){try{var detail=__instance.PlayerDetail;if(detail==null||detail.PlayerCharacter==null)return;int energy=ReferenceRules34.Energy(detail.PlayerCharacter.Agility);var prop=__instance.GetType().GetProperty("Energy",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(prop!=null&&prop.CanWrite){prop.SetValue(__instance,energy,null);return;}for(var t=__instance.GetType();t!=null;t=t.BaseType){var f=t.GetField("m_energy",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(f!=null){f.SetValue(__instance,energy);return;}}}catch(Exception e){Console.WriteLine("ENERGY FIX: "+e.GetType().Name);}}
 // PvE 普通怪 SimpleNpc.Reset 原本从不设置 Defence(默认 10), 导致防御减免失效、
 // 玩家对怪伤害偏高。补上 NpcInfo.Defence, 与 SimpleBoss 行为一致。
 static void NpcDefenceFix(SimpleNpc __instance){try{var npc=__instance.NpcInfo;if(npc!=null)__instance.Defence=Convert.ToDouble(npc.Defence);}catch(Exception e){Console.WriteLine("NPC DEFENCE FIX: "+e.GetType().Name);}}
 static int Main(string[] args){try{
  Console.SetIn(new ResidentInput36());root=FindRoot();RefreshProfiles();
  CombatDamage34.Configure(root);CombatDamage34.Install();
  BattleTransport36.Install();
  var harmony=new Harmony("ddtank36.local.battle.ai");harmony.Patch(typeof(Player).GetMethod("StartAttacking",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly),postfix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("AfterStartAttacking",BindingFlags.NonPublic|BindingFlags.Static)));
  TakeDamageFix36.Install(harmony);
  BattleAudit36.Install(harmony,typeof(Player),root);
  DamageReceiveAudit36.Install(harmony,typeof(Player).Assembly,root);
  var playerReset=typeof(Player).GetMethod("Reset",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(playerReset!=null)harmony.Patch(playerReset,postfix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("CombatEnergyFix",BindingFlags.NonPublic|BindingFlags.Static)));
  var prepareTurn=typeof(Player).GetMethod("PrepareNewTurn",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(prepareTurn!=null)harmony.Patch(prepareTurn,postfix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("CombatEnergyFix",BindingFlags.NonPublic|BindingFlags.Static)));
  var npcReset=typeof(SimpleNpc).GetMethod("Reset",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(npcReset!=null)harmony.Patch(npcReset,postfix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("NpcDefenceFix",BindingFlags.NonPublic|BindingFlags.Static)));
  foreach(var type in new[]{typeof(PVPGame),typeof(PVEGame)})foreach(string method in new[]{"NextTurn","GameOver"}){var target=type.GetMethod(method,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(target!=null)harmony.Patch(target,prefix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("BeforeTurnEnds",BindingFlags.NonPublic|BindingFlags.Static)));}
  string original=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fighting.Service.exe");var assembly=Assembly.LoadFrom(original);
  BattleAudit36.WatchGames(harmony,assembly);
  var matcher=assembly.GetType("Fighting.Server.Rooms.ProxyRoomMgr").GetMethod("StartMatchGame",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);harmony.Patch(matcher,prefix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("GuardMatch",BindingFlags.NonPublic|BindingFlags.Static)));
  var npcFallback=assembly.GetType("Fighting.Server.ServerClient").GetMethod("SendBeginFightNpc",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);harmony.Patch(npcFallback,prefix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("ResidentOpponentsOnly",BindingFlags.NonPublic|BindingFlags.Static)));
  var initialNpc=assembly.GetType("Fighting.Server.Rooms.ProxyRoomMgr").GetMethod("StartWithNpcUnsafe",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);harmony.Patch(initialNpc,prefix:new HarmonyMethod(typeof(LocalFightHost).GetMethod("ResidentOpponentsOnly",BindingFlags.NonPublic|BindingFlags.Static)));
  Console.WriteLine("DDTank36 battle AI loaded: "+bots.Count+" bots");assembly.EntryPoint.Invoke(null,new object[]{args.Length>0?args:new[]{"--start"}});return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static string FindRoot(){string configured=Environment.GetEnvironmentVariable("DDT36_ROOT");if(!String.IsNullOrEmpty(configured))return configured;var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(directory!=null){if(File.Exists(Path.Combine(directory.FullName,"config","applicationhost.config")))return directory.FullName;directory=directory.Parent;}throw new Exception("Deployment root not found");}
}
