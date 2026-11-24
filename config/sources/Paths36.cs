using System;
using System.IO;
namespace Local36 {
 public static class Paths36 {
  public const string Db=@"Data Source=(localdb)\DDTank36;Initial Catalog=Db_Tank_S1;Integrated Security=True";
  public static readonly string Root=FindRoot();
  static string FindRoot(){
   string configured=Environment.GetEnvironmentVariable("DDT36_ROOT");
   if(!String.IsNullOrWhiteSpace(configured))return Path.GetFullPath(configured);
   foreach(string origin in new[]{AppDomain.CurrentDomain.BaseDirectory,Directory.GetCurrentDirectory()}){
    var directory=new DirectoryInfo(origin);
    while(directory!=null){if(File.Exists(Path.Combine(directory.FullName,"config","applicationhost.config")))return directory.FullName;directory=directory.Parent;}
   }
   throw new InvalidOperationException("Cannot locate DDTank36 root; set DDT36_ROOT or keep config/applicationhost.config beside the package directory.");
  }
 }
}
