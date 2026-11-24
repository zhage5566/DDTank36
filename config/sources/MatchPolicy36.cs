using System;
using System.Collections.Generic;
using System.Linq;
namespace Local36 {
 public sealed class MatchMember36 { public int Id,Level,Power; }
 public static class MatchPolicy36 {
  public static bool MemberCompatible(MatchMember36 a,MatchMember36 b){
   return Math.Abs(a.Level-b.Level)<=5 && a.Power>0 && b.Power>0 && (double)Math.Max(a.Power,b.Power)/Math.Min(a.Power,b.Power)<=1.5;
  }
  public static bool TeamsCompatible(IEnumerable<MatchMember36> left,IEnumerable<MatchMember36> right){
   var a=left.OrderBy(x=>x.Level).ThenBy(x=>x.Power).ToArray();var b=right.OrderBy(x=>x.Level).ThenBy(x=>x.Power).ToArray();
   if(a.Length==0||a.Length!=b.Length)return false;
   if(Math.Abs(a.Average(x=>x.Level)-b.Average(x=>x.Level))>3)return false;
   double ap=a.Sum(x=>(double)x.Power),bp=b.Sum(x=>(double)x.Power);if(ap<=0||bp<=0||Math.Max(ap,bp)/Math.Min(ap,bp)>1.3)return false;
   return a.Zip(b,(x,y)=>MemberCompatible(x,y)).All(x=>x);
  }
  public static MatchMember36[] Select(IEnumerable<MatchMember36> team,IEnumerable<MatchMember36> candidates){
   var members=team.ToArray();if(members.Length<1||members.Length>4)return new MatchMember36[0];
   var own=new HashSet<int>(members.Select(m=>m.Id));var available=candidates.Where(c=>!own.Contains(c.Id)).GroupBy(c=>c.Id).Select(g=>g.First()).ToArray();
   var choices=members.Select(m=>available.Where(c=>MemberCompatible(m,c)).OrderBy(c=>Math.Abs(m.Level-c.Level)/5.0+Math.Abs(Math.Log((double)c.Power/m.Power))).ThenBy(c=>c.Id).ToArray()).OrderBy(c=>c.Length).ToArray();
   if(choices.Any(c=>c.Length==0))return new MatchMember36[0];
   // Greedy strongest-first can consume the only opponent for a weaker member.
   // At most four seats: try constrained seats first, with a bounded search.
   var selected=new List<MatchMember36>();var used=new HashSet<int>();int budget=32768;
   return Search(members,choices,0,selected,used,ref budget)?selected.ToArray():new MatchMember36[0];
  }
  static bool Search(MatchMember36[] team,MatchMember36[][] choices,int seat,List<MatchMember36> selected,HashSet<int> used,ref int budget){
   if(--budget<0)return false;if(seat==choices.Length)return TeamsCompatible(team,selected);
   foreach(var member in choices[seat]){if(!used.Add(member.Id))continue;selected.Add(member);
    if(Search(team,choices,seat+1,selected,used,ref budget))return true;
    selected.RemoveAt(selected.Count-1);used.Remove(member.Id);if(budget<0)break;
   }return false;
  }
  public static bool PowerTeamsCompatible(IEnumerable<MatchMember36> left,IEnumerable<MatchMember36> right){var a=left.OrderBy(x=>x.Power).ToArray();var b=right.OrderBy(x=>x.Power).ToArray();if(a.Length==0||a.Length!=b.Length)return false;double ap=a.Sum(x=>(double)x.Power),bp=b.Sum(x=>(double)x.Power);return ap>0&&bp>0&&Math.Max(ap,bp)/Math.Min(ap,bp)<=1.3&&a.Zip(b,(x,y)=>x.Power>0&&y.Power>0&&(double)Math.Max(x.Power,y.Power)/Math.Min(x.Power,y.Power)<=1.5).All(x=>x);}
  // Only choose existing lobby members. A missing power band falls back to the
  // strongest available residents; it never provisions items or copies players.
  public static MatchMember36[] SelectExisting(IEnumerable<MatchMember36> team,IEnumerable<MatchMember36> candidates){
   var own=team.ToArray();if(own.Length<1||own.Length>4)return new MatchMember36[0];var ids=new HashSet<int>(own.Select(p=>p.Id));
   var available=candidates.Where(p=>p.Power>0&&!ids.Contains(p.Id)).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();if(available.Length<own.Length)return new MatchMember36[0];
   var choices=own.Select(p=>available.Where(q=>p.Power>0&&(double)Math.Max(p.Power,q.Power)/Math.Min(p.Power,q.Power)<=1.5).OrderBy(q=>Math.Abs(Math.Log((double)q.Power/p.Power))).ThenBy(q=>Math.Abs(q.Level-p.Level)).ThenBy(q=>q.Id).ToArray()).OrderBy(p=>p.Length).ToArray();
   var selected=new List<MatchMember36>();int budget=32768;if(choices.All(p=>p.Length>0)&&SearchPower(own,choices,0,selected,new HashSet<int>(),ref budget))return selected.ToArray();
   return available.OrderByDescending(p=>p.Power).ThenByDescending(p=>p.Level).ThenBy(p=>p.Id).Take(own.Length).ToArray();
  }
  static bool SearchPower(MatchMember36[] own,MatchMember36[][] choices,int seat,List<MatchMember36> selected,HashSet<int> used,ref int budget){if(--budget<0)return false;if(seat==choices.Length)return PowerTeamsCompatible(own,selected);foreach(var member in choices[seat]){if(!used.Add(member.Id))continue;selected.Add(member);if(SearchPower(own,choices,seat+1,selected,used,ref budget))return true;selected.RemoveAt(selected.Count-1);used.Remove(member.Id);if(budget<0)break;}return false;}
 }
}
