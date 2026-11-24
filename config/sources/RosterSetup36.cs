using System;
using Local36;
class RosterSetup36 {
 static int Main(string[] args){try{if(args.Length>0)Environment.SetEnvironmentVariable("DDT36_ROOT",System.IO.Path.GetFullPath(args[0]));BotCatalog.EnsureRoster(200);Console.WriteLine("Roster ready: "+BotCatalog.ListProfiles().Count+". Online capacity: 50; rotation interval: 3-4 hours.");return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
}
