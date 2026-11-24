using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Server.GameObjects;
using SqlDataProvider.Data;
using Bussiness.Managers;
namespace Local36 {
 public static class StrictDerived34 {
  static readonly List<NativeMethodHook36> Hooks=new List<NativeMethodHook36>();static Func<GamePlayer,double> OriginalDamage,OriginalArmor;static Action<GamePlayer,int,int,int,int,int> OriginalProperties;static bool Installed;
  delegate void GemCall(GamePlayer player,int id,ref int value);
  static GemCall OriginalAttackGem,OriginalArmorGem;
  [ThreadStatic]static bool EquipmentScope;
  sealed class HpInput {public int Flat;public HpInput(){}}
  static readonly ConditionalWeakTable<GamePlayer,HpInput> HpInputs=new ConditionalWeakTable<GamePlayer,HpInput>();
  static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
  public static bool Ready(GamePlayer p){return p!=null&&p.Actives!=null&&p.Actives.Info!=null;}
  public static void RefreshBlood(GamePlayer p){HpInput input;if(!HpInputs.TryGetValue(p,out input))return;var c=p.PlayerCharacter;lock(input)c.Blood=(int)((950+c.Grade*50+ReferenceRules34.LevelBlood(c.Grade)+c.Defence+input.Flat)*p.GetBaseBlood());}
  public static object BloodInputs(GamePlayer p){HpInput input;if(!HpInputs.TryGetValue(p,out input))return null;var c=p.PlayerCharacter;lock(input)return new{grade=c.Grade,levelFoundation=950+c.Grade*50+ReferenceRules34.LevelBlood(c.Grade),defence=c.Defence,defenceBlood=c.Defence,nativeFlatSource=input.Flat,multiplier=p.GetBaseBlood(),expected=(int)((950+c.Grade*50+ReferenceRules34.LevelBlood(c.Grade)+c.Defence+input.Flat)*p.GetBaseBlood()),actual=c.Blood};}
  public static void Install(){if(Installed)return;try{
   Hook("BaseAttack","AttackGem",h=>OriginalAttackGem=h.Original<GemCall>());
   Hook("BaseDefence","ArmorGem",h=>OriginalArmorGem=h.Original<GemCall>());
   Hook("GetBaseAttack","Damage",h=>OriginalDamage=h.Original<Func<GamePlayer,double>>());
   Hook("GetBaseDefence","Armor",h=>OriginalArmor=h.Original<Func<GamePlayer,double>>());
   Hook("get_LevelPlusBlood","LevelBlood",null);
   Hook("UpdateBaseProperties","Properties",h=>OriginalProperties=h.Original<Action<GamePlayer,int,int,int,int,int>>());
   Hook("UpdateFightPower","Power",null);Installed=true;
   Console.WriteLine("STRICT 3.4 derived rules installed; original 3.6 system sources retained through native trampolines");
  }catch{foreach(var hook in Hooks)hook.Dispose();Hooks.Clear();throw;}}
  static void Hook(string name,string replacement,Action<NativeMethodHook36> original){var hook=new NativeMethodHook36(typeof(GamePlayer).GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance),typeof(StrictDerived34).GetMethod(replacement,Static));Hooks.Add(hook);hook.Apply();if(original!=null)original(hook);}
  static int[] Holes(ItemInfo i){return i==null?new int[0]:new[]{i.Hole1,i.Hole2,i.Hole3,i.Hole4,i.Hole5,i.Hole6};}
  static int GemArmor(ItemInfo item){int result=0;foreach(int id in Holes(item)){if(id<=0)continue;var row=ItemMgr.FindItemTemplate(id);if(row!=null&&row.CategoryID==11&&row.Property1==31&&row.Property2==3)result+=row.Property8;}return result;}
  static int GemDamage(ItemInfo item){int result=0;foreach(int id in Holes(item)){if(id<=0)continue;var row=ItemMgr.FindItemTemplate(id);if(row!=null&&row.CategoryID==11&&row.Property1==31&&row.Property2==3)result+=row.Property7;}return result;}
  [MethodImpl(MethodImplOptions.NoInlining)]static void AttackGem(GamePlayer p,int id,ref int value){if(!EquipmentScope)OriginalAttackGem(p,id,ref value);}
  [MethodImpl(MethodImplOptions.NoInlining)]static void ArmorGem(GamePlayer p,int id,ref int value){if(!EquipmentScope)OriginalArmorGem(p,id,ref value);}
  static double WithoutEquipmentGems(GamePlayer p,Func<GamePlayer,double> original){bool previous=EquipmentScope;EquipmentScope=true;try{return original(p);}finally{EquipmentScope=previous;}}
  [MethodImpl(MethodImplOptions.NoInlining)]static double Damage(GamePlayer p){
   double all=WithoutEquipmentGems(p,OriginalDamage)+ReferenceRules34.AttackToDamage(p.PlayerCharacter.Attack);var weapon=p.MainBag.GetItemAt(6);if(weapon==null)return all;
   double core=weapon.Template.Property7*Math.Pow(1.1,weapon.StrengthenLevel);
   // 3.6 rounds its equipment component; 3.4 retains the double. The
   // original call above still computes every live flat system/stone bonus.
   return all+core-Math.Round(core)+GemDamage(weapon);
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static double Armor(GamePlayer p){
   double all=WithoutEquipmentGems(p,OriginalArmor)+ReferenceRules34.DefenceToArmor(p.PlayerCharacter.Defence);var hat=p.MainBag.GetItemAt(0);var cloth=p.MainBag.GetItemAt(4);
   double oldCore=0;int newCore=0;
   if(hat!=null){oldCore+=Math.Round(hat.Template.Property7*Math.Pow(1.1,hat.StrengthenLevel));newCore+=hat.Template.Property7*(int)Math.Pow(1.1,hat.StrengthenLevel);}
   if(cloth!=null){oldCore+=Math.Round(cloth.Template.Property7*Math.Pow(1.1,cloth.StrengthenLevel));newCore+=cloth.Template.Property7*(int)Math.Pow(1.1,cloth.StrengthenLevel);}
   // The supplied reference overwrites hat-hole armor when assigning the hat
   // foundation. Retain that exact cast/order instead of silently correcting it.
   // Native Frank getters count armor sockets twice and even add their armor
   // to damage. Suppress only those helper contributions in the scoped original
   // call, then add the supplied 3.4 socket rule exactly once.
   return all-oldCore+newCore+GemArmor(cloth);
  }
  // Frank's getter represents the entire level foundation, whereas 3.4's
  // identically named getter is only the extra amount above 950 + grade*50.
  [MethodImpl(MethodImplOptions.NoInlining)]static int LevelBlood(GamePlayer p){int grade=p.PlayerCharacter.Grade;return 950+grade*50+ReferenceRules34.LevelBlood(grade);}
  [MethodImpl(MethodImplOptions.NoInlining)]static void Properties(GamePlayer p,int attack,int defence,int agility,int luck,int blood){
   bool complete=false,changed=false;
   try{OriginalProperties(p,attack,defence,agility,luck,blood);complete=true;}
   finally{var input=HpInputs.GetOrCreateValue(p);lock(input)input.Flat=blood;int previous=p.PlayerCharacter.Blood;RefreshBlood(p);changed=previous!=p.PlayerCharacter.Blood;}
   if(complete&&Ready(p)){int previous=p.PlayerCharacter.FightPower;CombatPower36.Apply(p);changed=changed||previous!=p.PlayerCharacter.FightPower;}
   if(complete&&changed)p.UpdateProperties();
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static void Power(GamePlayer p){if(!Ready(p))return;CombatPower36.Apply(p);p.OnPlayerPropertyChanged(p.PlayerCharacter);}
 }
}
