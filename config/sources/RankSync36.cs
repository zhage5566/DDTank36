using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Game.Server.GameObjects;
namespace Local36 {
 public static class RankSync36 {
  static readonly object Gate=new object();static readonly Dictionary<int,string> Seen=new Dictionary<int,string>();static bool Dirty,Running;static DateTime Next;
  public static void MarkDirty(){lock(Gate)Dirty=true;}
  public static void Observe(GamePlayer player){string value=player.PlayerCharacter.Grade+"/"+player.PlayerCharacter.FightPower;lock(Gate){string old;if(!Seen.TryGetValue(player.PlayerId,out old)){Seen[player.PlayerId]=value;return;}if(old==value)return;}lock(player){if(player.CurrentRoom!=null&&player.CurrentRoom.IsPlaying)return;NativeStats36.Recalculate(player);if(!player.SaveIntoDatabase())throw new InvalidOperationException("Native rank-source save failed");value=player.PlayerCharacter.Grade+"/"+player.PlayerCharacter.FightPower;}lock(Gate){Seen[player.PlayerId]=value;Dirty=true;}}
  public static void Tick(){lock(Gate){if(!Dirty||Running||DateTime.UtcNow<Next)return;Dirty=false;Running=true;Next=DateTime.UtcNow.AddSeconds(30);}ThreadPool.QueueUserWorkItem(_=>{try{string script=Path.Combine(Paths36.Root,"Refresh-NativeRankings36.ps1");var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Root \""+Paths36.Root+"\" -Apply"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Paths36.Root,RedirectStandardOutput=true,RedirectStandardError=true};using(var process=Process.Start(info)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();string text=output.Result+error.Result;File.AppendAllText(Path.Combine(Paths36.Root,"logs","ranking-refresh-live.log"),DateTime.UtcNow.ToString("o")+"\r\n"+text+"\r\n");if(process.ExitCode!=0)throw new InvalidOperationException("Native rank generators failed; see ranking-refresh-live.log");}Console.WriteLine("BOT RANK CACHE: refreshed from native persisted sources");}catch(Exception e){Console.WriteLine("BOT RANK CACHE: "+e.Message);lock(Gate)Dirty=true;}finally{lock(Gate)Running=false;}});}
 }
}
