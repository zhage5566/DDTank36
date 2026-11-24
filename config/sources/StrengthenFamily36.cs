using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
namespace Local36 {
 // Canonical identity comes from original Item_Strengthen_Goods, never from
 // guessed template-ID arithmetic or picture suffixes.
 public static class StrengthenFamily36 {
  sealed class Row {public int level,current,gain,origin;}
  static readonly object Gate=new object();static Row[] Rows;
  static Row[] Read(){lock(Gate){if(Rows==null){var rows=new List<Row>();using(var db=new SqlConnection(Paths36.Db)){db.Open();using(var q=new SqlCommand("SELECT Level,CurrentEquip,GainEquip,OrginEquip FROM Item_Strengthen_Goods",db))using(var r=q.ExecuteReader())while(r.Read())rows.Add(new Row{level=r.GetInt32(0),current=r.GetInt32(1),gain=r.GetInt32(2),origin=r.GetInt32(3)});}Rows=rows.ToArray();}return Rows;}}
  public static int Origin(int template){var families=Read().Where(r=>r.current==template||r.gain==template||r.origin==template).Select(r=>r.origin).Distinct().ToArray();if(families.Length>1)throw new InvalidOperationException("Ambiguous native strengthening template family: "+template);return families.Length==0?template:families[0];}
  public static int Resolve(int template,int level){int origin=Origin(template);var rows=Read().Where(r=>r.origin==origin&&r.current==origin&&r.level<=level).OrderByDescending(r=>r.level).ToArray();if(rows.Length==0)return origin;int target=rows[0].gain;if(target<=0)throw new InvalidOperationException("Invalid native strengthening identity");return target;}
 }
}
