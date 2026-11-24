using System;
using System.Configuration;
class ProbeConfig {
 static void Main(string[] args) {
  try {
   var c=ConfigurationManager.OpenExeConfiguration(args[0]);
   Console.WriteLine(c.AppSettings.Settings.Count);
   Console.WriteLine(c.GetSection("entityFramework"));
   foreach(ConnectionStringSettings s in c.ConnectionStrings.ConnectionStrings) Console.WriteLine(s.Name);
  } catch(Exception ex) {Console.WriteLine(ex);Environment.ExitCode=1;}
 }
}
