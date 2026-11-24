using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace Local36 {
 public sealed class PoolState36 {
  public int Version=1;
  public int Cursor;
  public int Capacity=50;
  public List<int> DesiredIds=new List<int>();
  public string NextRotationUtc;
  public long Generation;
 }
 public sealed class PoolScheduler36 {
  readonly string path;readonly Random random=new Random();readonly JavaScriptSerializer json=new JavaScriptSerializer();
  int[] roster=new int[0];public PoolState36 State{get;private set;}
  public PoolScheduler36(string file){path=file;try{State=json.Deserialize<PoolState36>(File.ReadAllText(path));}catch{State=null;}if(State==null)State=new PoolState36();if(State.DesiredIds==null)State.DesiredIds=new List<int>();}
  public void UpdateRoster(IEnumerable<int> ids){
   roster=ids.Distinct().OrderBy(x=>x).ToArray();State.Capacity=50;
   State.DesiredIds=State.DesiredIds.Where(x=>roster.Contains(x)).Distinct().Take(State.Capacity).ToList();
   if(State.Cursor<0||State.Cursor>=Math.Max(1,roster.Length))State.Cursor=0;
   Fill(State.DesiredIds);
   DateTime next;if(!DateTime.TryParse(State.NextRotationUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out next))SetNext(DateTime.UtcNow);
   Save();
  }
  void Fill(List<int> result){for(int step=0;result.Count<Math.Min(State.Capacity,roster.Length)&&step<roster.Length;step++){
   int id=roster[State.Cursor];State.Cursor=(State.Cursor+1)%roster.Length;if(!result.Contains(id))result.Add(id);
  }}
  public bool Advance(DateTime utc){
   if(roster.Length==0)return false;
   DateTime next=DateTime.Parse(State.NextRotationUtc,null,System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
   if(utc<next)return false;
   State.DesiredIds.Clear();Fill(State.DesiredIds);State.Generation++;SetNext(utc);Save();return true;
  }
  void SetNext(DateTime utc){State.NextRotationUtc=utc.AddMinutes(180+random.Next(61)).ToString("o");}
  public bool Promote(IEnumerable<int> requested,IEnumerable<int> idle){
   var incoming=requested.Distinct().Where(id=>roster.Contains(id)&&!State.DesiredIds.Contains(id)).ToArray();
   if(incoming.Length==0)return true;
   var outgoing=idle.Distinct().Where(id=>State.DesiredIds.Contains(id)&&!requested.Contains(id)).Take(incoming.Length).ToArray();
   int free=Math.Max(0,State.Capacity-State.DesiredIds.Count);
   if(outgoing.Length+free<incoming.Length)return false;
   foreach(int id in outgoing.Take(Math.Max(0,incoming.Length-free)))State.DesiredIds.Remove(id);
   State.DesiredIds.AddRange(incoming);Save();return true;
  }
  public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,json.Serialize(State));if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);}
 }
}
