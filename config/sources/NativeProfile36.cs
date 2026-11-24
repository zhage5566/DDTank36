using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Net;
using System.Net.Sockets;
using Game.Server;
using Game.Base.Packets;
using Game.Server.GameObjects;
namespace Local36 {
 // Controlled diagnostic of derived native properties. Never changes a source
 // item, Texp/Pet/Card/Totem record, currency or experience; never saves a player.
 public static class NativeProfile36 {
  [StructLayout(LayoutKind.Sequential)]struct MemoryRegion {public IntPtr BaseAddress,AllocationBase;public uint AllocationProtect;public UIntPtr RegionSize;public uint State,Protect,Type;}
  [DllImport("kernel32.dll")]static extern UIntPtr VirtualQuery(IntPtr address,out MemoryRegion region,UIntPtr length);
  static byte[] ReadCode(IntPtr address,int wanted){MemoryRegion region;if(address==IntPtr.Zero||VirtualQuery(address,out region,(UIntPtr)Marshal.SizeOf(typeof(MemoryRegion)))==UIntPtr.Zero||region.State!=0x1000||(region.Protect&0x101)!=0)return null;long remaining=checked(region.BaseAddress.ToInt64()+(long)region.RegionSize.ToUInt64()-address.ToInt64());if(remaining<=0)return null;var bytes=new byte[(int)Math.Min(wanted,remaining)];Marshal.Copy(address,bytes,0,bytes.Length);return bytes;}
  static string Address(IntPtr value){return "0x"+value.ToInt64().ToString("X");}
  static object SampleMethod(MethodInfo method,int length){
   RuntimeHelpers.PrepareMethod(method.MethodHandle);var entry=method.MethodHandle.GetFunctionPointer();var address=entry;var blocks=new List<object>();
   for(int depth=0;depth<4;depth++){
    var bytes=ReadCode(address,length);if(bytes==null){blocks.Add(new{address=Address(address),error="Region is not committed/readable"});break;}
    var references=new List<object>();
    // Bounded RIP-relative call/jump slots and scalar-double constants from the
    // sampled method only. All referenced reads also pass VirtualQuery checks.
    if(IntPtr.Size==8)for(int n=0;n+6<=bytes.Length;n++){
     if(bytes[n]==0xFF&&(bytes[n+1]==0x15||bytes[n+1]==0x25)){
      var slot=new IntPtr(address.ToInt64()+n+6+BitConverter.ToInt32(bytes,n+2));var data=ReadCode(slot,8);if(data!=null&&data.Length==8)references.Add(new{offset=n,kind="indirectCallOrJump",address=Address(slot),hex=BitConverter.ToString(data).Replace("-",""),target="0x"+BitConverter.ToInt64(data,0).ToString("X")});
     }
     if(n+8<=bytes.Length&&bytes[n]==0xF2&&bytes[n+1]==0x0F&&(bytes[n+2]==0x10||bytes[n+2]==0x58||bytes[n+2]==0x59||bytes[n+2]==0x5C||bytes[n+2]==0x5E)&&(bytes[n+3]&0xC7)==0x05){
      var slot=new IntPtr(address.ToInt64()+n+8+BitConverter.ToInt32(bytes,n+4));var data=ReadCode(slot,8);if(data!=null&&data.Length==8)references.Add(new{offset=n,kind="scalarDoubleConstant",address=Address(slot),hex=BitConverter.ToString(data).Replace("-",""),value=BitConverter.ToDouble(data,0).ToString("R",System.Globalization.CultureInfo.InvariantCulture)});
     }
    }
    blocks.Add(new{address=Address(address),hex=BitConverter.ToString(bytes).Replace("-",""),references=references.ToArray()});IntPtr next=IntPtr.Zero;
    if(bytes.Length>=5&&bytes[0]==0xE9)next=new IntPtr(address.ToInt64()+5+BitConverter.ToInt32(bytes,1));
    else if(IntPtr.Size==8&&bytes.Length>=6&&bytes[0]==0xFF&&bytes[1]==0x25){var slot=new IntPtr(address.ToInt64()+6+BitConverter.ToInt32(bytes,2));var data=ReadCode(slot,8);if(data!=null&&data.Length==8)next=new IntPtr(BitConverter.ToInt64(data,0));}
    else if(IntPtr.Size==8&&bytes.Length>=12&&bytes[0]==0x48&&bytes[1]==0xB8&&bytes[10]==0xFF&&bytes[11]==0xE0)next=new IntPtr(BitConverter.ToInt64(bytes,2));
    if(next==IntPtr.Zero||next==address)break;address=next;
   }
   return new{method=method.DeclaringType.FullName+"."+method.Name,entry=Address(entry),blocks=blocks.ToArray()};
  }
  public static object PowerCode(){
   var methods=new List<object>();foreach(string name in new[]{"UpdateFightPower","GetBaseAttack","GetBaseDefence"})methods.Add(SampleMethod(typeof(GamePlayer).GetMethod(name),name=="UpdateFightPower"?4096:2048));
   foreach(string name in new[]{"Attack","Defence","Agility","Luck","Blood","FightPower"}){var prop=typeof(SqlDataProvider.Data.PlayerInfo).GetProperty(name);methods.Add(SampleMethod(prop.GetGetMethod(),128));if(name=="FightPower")methods.Add(SampleMethod(prop.GetSetMethod(),128));}
   foreach(string name in new[]{"totalDamage","totalArmor"})methods.Add(SampleMethod(typeof(Game.Server.GameUtils.PlayerProInventory).GetProperty(name).GetGetMethod(),128));
   return new{readOnly=true,saved=false,architecture=IntPtr.Size==8?"x64":"x86",methods=methods.ToArray()};
  }
  public static object LevelCode(){
   var methods=new List<object>();foreach(string name in new[]{"set_Level","UpdateLevel","UpdateBaseProperties","OnLevelUp","Login","get_PlayerState","set_PlayerState"}){var method=typeof(GamePlayer).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null)methods.Add(SampleMethod(method,name=="OnLevelUp"?4096:16384));}
   methods.Add(SampleMethod(typeof(Game.Server.GameUtils.PlayerEquipInventory).GetMethod("UpdatePlayerProperties"),65536));
   return new{readOnly=true,saved=false,architecture=IntPtr.Size==8?"x64":"x86",methods=methods.ToArray()};
  }
  sealed class IsolatedClient:GameClient {
   public int sentPackets;
   public IsolatedClient():base(GameServer.Instance,new byte[8192],new byte[8192]){Out=new AbstractPacketLib(this);}
   public override void SendTCP(GSPacketIn packet){sentPackets++;}
  }
  static object Scalar(object value){if(value==null)return null;var type=value.GetType();return type.IsPrimitive||value is string||value is decimal?(object)value:value.ToString();}
  static object NativeFlags(object value){if(value==null)return null;var fields=new List<object>();for(var type=value.GetType();type!=null;type=type.BaseType)foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(field.FieldType==typeof(bool)||field.FieldType==typeof(int))fields.Add(new{declaringType=type.FullName,name=field.Name,value=field.GetValue(value)});return fields.ToArray();}
  static object Bag(GamePlayer p,Game.Server.GameUtils.PlayerEquipInventory bag){if(bag==null)return null;return new{bagType=bag.BagType,beginSlot=bag.BeginSlot,capacity=bag.Capalility,sameOwner=Object.ReferenceEquals(p,bag.Player),items=Enumerable.Range(0,Math.Min(200,bag.Capalility)).Select(slot=>bag.GetItemAt(slot)).Where(i=>i!=null).Select(i=>new{id=i.ItemID,slot=i.Place,template=i.TemplateID,strengthen=i.StrengthenLevel,attack=i.AttackCompose,defence=i.DefendCompose}).ToArray()};}
  public static object Snapshot(GamePlayer p){var c=p.PlayerCharacter;var t=c.Texp;var pet=p.PetBag.GetPetIsEquip();var cards=p.CardBag.GetEquipCard()??new List<SqlDataProvider.Data.UserCardInfo>();return new{
   userId=p.PlayerId,grade=c.Grade,playerLevel=p.Level,isFirst=c.IsFirst,attack=c.Attack,defence=c.Defence,agility=c.Agility,luck=c.Luck,blood=c.Blood,power=c.FightPower,
   active=p.IsActive,robot=p.IsRobot,playerState=(int)p.PlayerState,playerStateName=p.PlayerState.ToString(),clientConnected=p.Client!=null&&p.Client.IsConnected,mainEqualsEquip=Object.ReferenceEquals(p.MainBag,p.EquipBag),mainBag=Bag(p,p.MainBag),equipBag=Bag(p,p.EquipBag),
   nativeFlags=new{player=NativeFlags(p),mainBag=NativeFlags(p.MainBag),client=NativeFlags(p.Client)},
   baseAttack=p.GetBaseAttack(),baseDefence=p.GetBaseDefence(),baseAgility=p.GetBaseAgility(),baseBlood=p.GetBaseBlood(),goldBlood=p.GetGoldBlood(),levelBlood=p.LevelPlusBlood,strictBlood=StrictDerived34.BloodInputs(p),
   healstone=p.Healstone==null?null:new{template=p.Healstone.TemplateID,count=p.Healstone.Count,amount=p.Healstone.Template.Property2},
   temporary=p.TempProperties==null?null:p.TempProperties.OrderBy(v=>v.Key).Select(v=>new{key=v.Key,value=Scalar(v.Value),type=v.Value==null?null:v.Value.GetType().FullName}).ToArray(),
   nativeProperties=p.PlayerProp==null||p.PlayerProp.Current==null?null:p.PlayerProp.Current.OrderBy(v=>v.Key).Select(v=>new{source=v.Key,values=v.Value==null?null:v.Value.OrderBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Value)}).ToArray(),nativeTotalDamage=p.PlayerProp==null?0:p.PlayerProp.totalDamage,nativeTotalArmor=p.PlayerProp==null?0:p.PlayerProp.totalArmor,
   texp=t==null?null:new{hp=t.hpTexpExp,attack=t.attTexpExp,defence=t.defTexpExp,agility=t.spdTexpExp,luck=t.lukTexpExp},
   pet=pet==null?null:new{id=pet.ID,level=pet.Level,gp=pet.GP,equipped=pet.IsEquip,attack=pet.TotalAttack,defence=pet.TotalDefence,agility=pet.TotalAgility,luck=pet.TotalLuck,blood=pet.TotalBlood},
   cards=cards.Select(x=>new{id=x.CardID,level=x.Level,attack=x.TotalAttack,defence=x.TotalDefence,agility=x.TotalAgility,luck=x.TotalLuck}).ToArray(),
   totem=c.TotemId,totemUpgrade=c.TotemUpgrade,zhanling=c.zhanlingLevel,zhanlingExp=c.zhanlingExp,necklaceCast=c.necklaceCastLevel,necklaceExp=c.necklaceExp,fineSuitExp=c.fineSuitExp,
   equipment=Enumerable.Range(0,31).Select(slot=>p.MainBag.GetItemAt(slot)).Where(x=>x!=null).Select(x=>new{id=x.ItemID,slot=x.Place,template=x.TemplateID,strengthen=x.StrengthenLevel,attack=x.AttackCompose,defence=x.DefendCompose,agility=x.AgilityCompose,luck=x.LuckCompose,nativeAttack=x.Attack,nativeDefence=x.Defence,nativeAgility=x.Agility,nativeLuck=x.Luck,templateAttack=x.Template.Attack,templateDefence=x.Template.Defence,templateAgility=x.Template.Agility,templateLuck=x.Template.Luck,property7=x.Template.Property7,pic=x.Pic,templatePic=x.Template.Pic,category=x.Template.CategoryID,isGold=x.isGold,refinery=x.RefineryLevel,holes=new[]{x.Hole1,x.Hole2,x.Hole3,x.Hole4,x.Hole5,x.Hole6}}).ToArray()
  };}
  public static void RefreshNative(GamePlayer p){if(p==null)throw new ArgumentNullException("p");lock(p){p.MainBag.UpdatePlayerProperties();p.MainBag.UpdatePlayerElementProperties();p.UpdateProperties();p.UpdateFightPower();}}
  // Native aggregation only. Source equipment, experience and resources are
  // deliberately untouched so the caller can compare this with the bag path.
  public static object TraceAggregation(GamePlayer p){
   if(p==null||p.GetType().FullName!="Local36.BotPlayer")throw new InvalidOperationException("Only a local bot may be profiled");lock(p){
    if(p.CurrentRoom!=null||p.IsInWorldBossRoom||Lifecycle36.ForgeBusy(p))throw new InvalidOperationException("Bot must be idle outside native rooms and forge");
    if(p.PlayerProp==null)throw new InvalidOperationException("Native PlayerProp is not initialized");
    var stages=new List<object>();stages.Add(new{stage="before",profile=Snapshot(p)});
    p.PlayerProp.CreateBaseProp(true);stages.Add(new{stage="PlayerProp.CreateBaseProp(isSelf:true)",profile=Snapshot(p)});
    p.PlayerProp.ViewCurrent();stages.Add(new{stage="PlayerProp.ViewCurrent",profile=Snapshot(p)});
    p.UpdateProperties();stages.Add(new{stage="GamePlayer.UpdateProperties",profile=Snapshot(p)});
    p.UpdateFightPower();stages.Add(new{stage="GamePlayer.UpdateFightPower",profile=Snapshot(p)});
    return new{derivedOnly=true,saved=false,stages=stages.ToArray()};
   }
  }
  // Balanced native transaction: even a diagnostic failure cannot leave the
  // native change counter open. No source item or resource is written here.
  public static object TraceTransactions(GamePlayer p){
   if(p==null||p.GetType().FullName!="Local36.BotPlayer")throw new InvalidOperationException("Only a local bot may be profiled");lock(p){
    if(p.CurrentRoom!=null||p.IsInWorldBossRoom||Lifecycle36.ForgeBusy(p))throw new InvalidOperationException("Bot must be idle outside native rooms and forge");
    var stages=new List<object>();stages.Add(new{stage="before",profile=Snapshot(p)});bool began=false;
    try{
     p.BeginAllChanges();began=true;stages.Add(new{stage="GamePlayer.BeginAllChanges",profile=Snapshot(p)});
     p.MainBag.UpdatePlayerProperties();stages.Add(new{stage="MainBag.UpdatePlayerProperties",profile=Snapshot(p)});
     p.MainBag.UpdatePlayerElementProperties();stages.Add(new{stage="MainBag.UpdatePlayerElementProperties",profile=Snapshot(p)});
     p.UpdateProperties();p.UpdateFightPower();stages.Add(new{stage="GamePlayer.UpdateProperties+UpdateFightPower.beforeCommit",profile=Snapshot(p)});
    }finally{if(began)p.CommitAllChanges();}
    stages.Add(new{stage="GamePlayer.CommitAllChanges",profile=Snapshot(p)});
    return new{derivedOnly=true,saved=false,balanced=true,stages=stages.ToArray()};
   }
  }
  public static object TraceNativeConnect(GamePlayer source){
   if(source==null||source.GetType().FullName!="Local36.BotPlayer")throw new InvalidOperationException("Only a local bot may be profiled");lock(source){
    if(source.CurrentRoom!=null||source.IsInWorldBossRoom||Lifecycle36.ForgeBusy(source))throw new InvalidOperationException("Bot must be idle outside native rooms and forge");
    var stages=new List<object>();var client=new IsolatedClient();var listener=new TcpListener(IPAddress.Loopback,0);Socket endpoint=null,peer=null;GamePlayer isolated=null;bool connected=false,loaded=false;string error=null;
    try{
     stages.Add(new{stage="isolatedClient.beforeConnect",connected=client.IsConnected,fields=NativeFlags(client)});
     listener.Start(1);endpoint=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);endpoint.Connect((IPEndPoint)listener.LocalEndpoint);peer=listener.AcceptSocket();
     connected=client.Connect(endpoint);stages.Add(new{stage="isolatedClient.afterNativeConnect",result=connected,connected=client.IsConnected,socketConnected=endpoint.Connected,fields=NativeFlags(client)});
     client.Version=5498628;stages.Add(new{stage="isolatedClient.actualAS3Version.beforeNativeLoad",version=client.Version,fields=NativeFlags(client)});
     using(var db=new Bussiness.PlayerBussiness()){
      var info=db.GetUserSingleByUserID(source.PlayerId);if(info==null)throw new InvalidOperationException("Diagnostic bot source was not found");
      isolated=new GamePlayer(info.ID,info.UserName,client,info);client.Player=isolated;loaded=isolated.LoadFromDatabase();
     }
     if(!loaded)throw new InvalidOperationException("Isolated native player LoadFromDatabase returned false");
     stages.Add(new{stage="isolatedPlayer.afterLoad",profile=Snapshot(isolated)});
     isolated.MainBag.UpdatePlayerProperties();stages.Add(new{stage="MainBag.UpdatePlayerProperties",profile=Snapshot(isolated)});
     isolated.MainBag.UpdatePlayerElementProperties();stages.Add(new{stage="MainBag.UpdatePlayerElementProperties",profile=Snapshot(isolated)});
     isolated.UpdateProperties();isolated.UpdateFightPower();stages.Add(new{stage="GamePlayer.UpdateProperties+UpdateFightPower",profile=Snapshot(isolated)});
     var originalPlayerState=isolated.PlayerState;isolated.PlayerState=Game.Server.Packets.ePlayerState.Online;
     isolated.MainBag.UpdatePlayerProperties();isolated.MainBag.UpdatePlayerElementProperties();isolated.UpdateFightPower();isolated.UpdateProperties();
     stages.Add(new{stage="isolated.nativePlayerState.Online+MainBag.UpdatePlayerProperties",experimentalOnly=true,sourceDataChanged=false,originalState=(int)originalPlayerState,profile=Snapshot(isolated)});
     isolated.PlayerProp.CreateBaseProp(true);isolated.PlayerProp.ViewCurrent();stages.Add(new{stage="PlayerProp.CreateBaseProp+ViewCurrent",profile=Snapshot(isolated)});
     int originalFirst=isolated.PlayerCharacter.IsFirst;
     try{foreach(int first in new[]{159,25,2,6}){isolated.PlayerCharacter.IsFirst=first;isolated.MainBag.UpdatePlayerProperties();isolated.UpdateProperties();isolated.UpdateFightPower();stages.Add(new{stage="isolated.IsFirst="+first+".nativeRefresh",experimentalOnly=true,profile=Snapshot(isolated)});}}
     finally{isolated.PlayerCharacter.IsFirst=originalFirst;}
     isolated.UpdateItemForUser(null);stages.Add(new{stage="isolated.UpdateItemForUser(null)",profile=Snapshot(isolated)});
     MethodInfo placeChanged=null;for(Type bagType=isolated.MainBag.GetType();bagType!=null&&placeChanged==null;bagType=bagType.BaseType)placeChanged=bagType.GetMethod("OnPlaceChanged",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,new[]{typeof(int)},null);
     if(placeChanged==null)throw new InvalidOperationException("Native bag OnPlaceChanged(int) was not found");
     bool bagBegan=false;try{isolated.MainBag.BeginChanges();bagBegan=true;for(int slot=0;slot<31;slot++)if(isolated.MainBag.GetItemAt(slot)!=null)placeChanged.Invoke(isolated.MainBag,new object[]{slot});}
     finally{if(bagBegan)isolated.MainBag.CommitChanges();}
     isolated.MainBag.UpdateChangedPlaces();isolated.UpdateProperties();isolated.UpdateFightPower();stages.Add(new{stage="isolated.MainBag.nativeOnPlaceChanged+CommitChanges+UpdateChangedPlaces",experimentalOnly=true,profile=Snapshot(isolated)});
     var aggregate=NativeStats36.Recalculate(isolated);stages.Add(new{stage="isolated.NativeStats36.Recalculate",sources=aggregate,profile=Snapshot(isolated)});
    }catch(Exception failure){error=failure.GetType().FullName+": "+failure.Message;}
    finally{
     // Native disconnect must see no attached player: the diagnostic object is
     // never registered in WorldMgr and must never trigger a disconnect save.
     client.Player=null;
     try{client.Disconnect();}finally{if(endpoint!=null)endpoint.Close();if(peer!=null)peer.Close();listener.Stop();}
    }
    return new{userId=source.PlayerId,isolated=true,localOnly=true,registered=false,saved=false,connected,loaded,error,packetsDiscarded=client.sentPackets,stages=stages.ToArray()};
   }
  }
  public static object TraceConnected(GamePlayer p){
   if(p==null||p.GetType().FullName!="Local36.BotPlayer")throw new InvalidOperationException("Only a local bot may be profiled");lock(p){
    if(p.CurrentRoom!=null||p.IsInWorldBossRoom||Lifecycle36.ForgeBusy(p))throw new InvalidOperationException("Bot must be idle outside native rooms and forge");
    FieldInfo socketField=null;for(Type type=p.Client.GetType();type!=null&&socketField==null;type=type.BaseType)socketField=type.GetField("m_sock",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(socketField==null||socketField.FieldType!=typeof(Socket))throw new InvalidOperationException("Native BaseClient socket field was not found");
    var original=(Socket)socketField.GetValue(p.Client);bool originalConnected=p.Client.IsConnected;var listener=new TcpListener(IPAddress.Loopback,0);Socket endpoint=null,peer=null;bool replaced=false;
    try{listener.Start(1);endpoint=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);endpoint.Connect((IPEndPoint)listener.LocalEndpoint);peer=listener.AcceptSocket();socketField.SetValue(p.Client,endpoint);replaced=true;bool connected=p.Client.IsConnected;var trace=Trace(p);return new{originalConnected,connected,nativeSocketConnected=endpoint.Connected,localOnly=true,saved=false,trace};}
    finally{try{if(replaced)socketField.SetValue(p.Client,original);}finally{if(endpoint!=null)endpoint.Close();if(peer!=null)peer.Close();listener.Stop();}}
   }
  }
  public static object Trace(GamePlayer p){if(p==null||p.GetType().FullName!="Local36.BotPlayer")throw new InvalidOperationException("Only a local bot may be profiled");lock(p){if(p.CurrentRoom!=null||p.IsInWorldBossRoom||Lifecycle36.ForgeBusy(p))throw new InvalidOperationException("Bot must be idle outside native rooms and forge");var stages=new List<object>();stages.Add(new{stage="before",profile=Snapshot(p)});p.MainBag.UpdatePlayerProperties();stages.Add(new{stage="MainBag.UpdatePlayerProperties",profile=Snapshot(p)});p.MainBag.UpdatePlayerElementProperties();stages.Add(new{stage="MainBag.UpdatePlayerElementProperties",profile=Snapshot(p)});p.UpdateProperties();stages.Add(new{stage="GamePlayer.UpdateProperties",profile=Snapshot(p)});p.UpdateFightPower();stages.Add(new{stage="GamePlayer.UpdateFightPower",profile=Snapshot(p)});if(!Object.ReferenceEquals(p.MainBag,p.EquipBag)){p.EquipBag.UpdatePlayerProperties();stages.Add(new{stage="EquipBag.UpdatePlayerProperties",profile=Snapshot(p)});p.MainBag.UpdatePlayerProperties();p.UpdateFightPower();stages.Add(new{stage="MainBag.recheck.after.EquipBag",profile=Snapshot(p)});}return new{derivedOnly=true,saved=false,stages=stages.ToArray()};}}
 }
}
