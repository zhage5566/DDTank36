using System;
using System.IO;
using System.Reflection;
internal static class CenterLocalHost36 {
 static int Main(string[] args){try{
  Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
  Console.SetIn(new ResidentInput36());
  var original=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Center.Service.exe"));
  original.EntryPoint.Invoke(null,new object[]{args.Length>0?args:new[]{"--start"}});
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
