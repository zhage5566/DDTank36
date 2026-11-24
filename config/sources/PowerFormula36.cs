using System;
namespace Local36 {
 public static class PowerFormula36 {
  // D:\DDTank-3.4\server\Road\Game.Server.dll, GamePlayer.UpdateFightPower.
  // HP is supplied by the complete 3.6 aggregator to retain its advanced systems.
  public static int Calculate(int attack,int defence,int agility,int luck,double damage,double armor,int blood){
   double sum=(double)attack+defence+agility+luck;
   double value=(sum+1000)*(damage*damage*damage+3.5*armor*armor*armor)/100000000.0+blood*0.95-950;
   if(Double.IsNaN(value)||Double.IsInfinity(value))throw new ArgumentException("Invalid native combat attributes");
   return (int)Math.Max(1,Math.Min(Int32.MaxValue,value));
  }
 }
}
