using System;
using System.Collections.Generic;
using System.Linq;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
namespace Local36 {
 // Observe the native public event; never rewrite VM-protected Road method IL.
 public static class CombatPower36 {
  sealed class Watch {public GamePlayer Player;public Delegate Handler;}
  static readonly object Gate=new object();static readonly Dictionary<int,Watch> Watches=new Dictionary<int,Watch>();
  [ThreadStatic]static HashSet<GamePlayer> Updating;
  public static int Apply(GamePlayer player){
   lock(player){if(Updating==null)Updating=new HashSet<GamePlayer>();if(!Updating.Add(player))return player.PlayerCharacter.FightPower;
    try{StrictDerived34.RefreshBlood(player);var c=player.PlayerCharacter;double damage=player.GetBaseAttack(),armor=player.GetBaseDefence();
     int value=PowerFormula36.Calculate(c.Attack,c.Defence,c.Agility,c.Luck,damage,armor,c.Blood);
     bool changed=c.FightPower!=value||player.FightPower!=value;player.FightPower=value;c.FightPower=value;
     if(changed){Console.WriteLine("COMBAT POWER 3.4: id="+player.PlayerId+" power="+value+" damage="+damage+" armor="+armor+" blood="+c.Blood+" attack="+c.Attack+" defence="+c.Defence+" agility="+c.Agility+" luck="+c.Luck+" energy="+ReferenceRules34.Energy(c.Agility));if(player.Out!=null&&player.PlayerProp!=null)player.Out.SendUpdatePlayerProperty(c,player.PlayerProp);}
     return value;
    }finally{Updating.Remove(player);}
   }
  }
  public static void Observe(IEnumerable<GamePlayer> players){
   var all=players.ToArray();lock(Gate){
    foreach(var pair in Watches.ToArray())if(!all.Any(p=>Object.ReferenceEquals(p,pair.Value.Player))){typeof(GamePlayer).GetEvent("PlayerPropertyChanged").RemoveEventHandler(pair.Value.Player,pair.Value.Handler);Watches.Remove(pair.Key);}
    foreach(var p in all){if(Watches.ContainsKey(p.PlayerId))continue;var player=p;
     Action<PlayerInfo> callback=info=>{Apply(player);};var ev=typeof(GamePlayer).GetEvent("PlayerPropertyChanged");
     var handler=Delegate.CreateDelegate(ev.EventHandlerType,callback.Target,callback.Method);ev.AddEventHandler(player,handler);Watches[player.PlayerId]=new Watch{Player=player,Handler=handler};
     Apply(player);player.UpdateProperties();
    }
   }
  }
 }
}
