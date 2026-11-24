using System;
namespace Local36 {
 // Literal scalar relationships recovered from the supplied 3.4 runtime DLLs.
 // Keep the reference's integer cast order; do not substitute 3.6 coefficients.
 public static class ReferenceRules34 {
  public static int LevelBlood(int grade){int value=0;for(int stage=10;stage<=60;stage+=10)if(grade>stage)value+=(grade-stage)*(stage+20);return value;}
  public static double NecklaceMultiplier(int property1){return (100.0+property1)/100.0;}
  public static int Blood(int grade,int defence,double necklaceMultiplier){return (int)((950+grade*50+LevelBlood(grade)+defence)*necklaceMultiplier);}
  public static int Energy(int agility){return agility/10+240;}
 // ---- Moderate direct-conversion rebalance (2026-10-17): attack/defence fold
 // directly into damage/armor via StrictDerived34's GetBaseAttack/GetBaseDefence
 // hooks. Kept apart from the recovered 3.4 coefficients above. ----
 public static double AttackToDamage(int attack){return attack*0.5;}
 public static double DefenceToArmor(int defence){return defence*0.5;}
  public static double WeaponDamage(int property7,int strengthen,int damageFromHoles){return property7*Math.Pow(1.1,strengthen)+damageFromHoles;}
  // The reference casts Pow BEFORE multiplication for armor, unlike damage.
  public static int Armor(int hatProperty7,int hatStrength,int clothProperty7,int clothStrength,int clothHoleArmor){return hatProperty7*(int)Math.Pow(1.1,hatStrength)+clothHoleArmor+clothProperty7*(int)Math.Pow(1.1,clothStrength);}
  public static int Power(int attack,int defence,int agility,int luck,double damage,double armor,int blood){return (int)(((double)attack+defence+agility+luck+1000)*(damage*damage*damage+3.5*armor*armor*armor)/100000000.0+blood*0.95-950);}
  public static double RawDamage(double baseDamage,double attack,int grade,double targetArmor,double targetDefence,double luck,float damagePlus,float shootMinus){
   double armorReduction=0.95*(targetArmor-3*grade)/(500+targetArmor-3*grade);
   double defenceReduction=targetDefence-luck<0?0:0.95*(targetDefence-luck)/(600+targetDefence-luck);
   return baseDamage*(1+attack*0.001)*(1-(armorReduction+defenceReduction-armorReduction*defenceReduction))*damagePlus*shootMinus;
  }
  public static int DirectDamage(double raw){return raw<0?1:(int)raw;}
  public static int BombDamage(double raw,double distance,int radius){if(distance>=radius)return 0;double value=raw*(1-distance/radius/4);return value<0?1:(int)value;}
  public static double CriticalChance(double luck){return luck*100/(800+luck);}
  public static int CriticalExtra(double luck,int damage,int random0To99){return CriticalChance(luck)>random0To99?(int)((0.5+luck*0.0005)*damage):0;}
 }
}
