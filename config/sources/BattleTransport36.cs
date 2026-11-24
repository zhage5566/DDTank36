using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Base;
namespace Local36 {
 public static class BattleTransport36 {
  const int Capacity=65536;
  static NativeMethodHook36 Hook,OutgoingHook;static Func<BaseClient,Socket,bool> Original;static Func<BaseConnector,bool> OriginalOutgoing;
  static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  static readonly FieldInfo ReadBuffer=typeof(BaseClient).GetField("m_readBuffer",Fields);
  public static void Install(){if(Hook!=null)return;Hook=new NativeMethodHook36(typeof(BaseClient).GetMethod("Connect",new[]{typeof(Socket)}),typeof(BattleTransport36).GetMethod("Connect",BindingFlags.Static|BindingFlags.NonPublic));Hook.Apply();Original=Hook.Original<Func<BaseClient,Socket,bool>>();OutgoingHook=new NativeMethodHook36(typeof(BaseConnector).GetMethod("Connect",Type.EmptyTypes),typeof(BattleTransport36).GetMethod("ConnectOutgoing",BindingFlags.Static|BindingFlags.NonPublic));OutgoingHook.Apply();OriginalOutgoing=OutgoingHook.Original<Func<BaseConnector,bool>>();Console.WriteLine("Battle transport receive capacity enabled for incoming and outgoing battle links only: "+Capacity);}
  public static void Prepare(BaseClient client){
   string name=client.GetType().FullName;
   if(name!="Game.Server.Battle.FightServerConnector"&&name!="Fighting.Server.ServerClient")return;
   var old=(byte[])ReadBuffer.GetValue(client);if(old==null||old.Length>=Capacity)return;
   var expanded=new byte[Capacity];Buffer.BlockCopy(old,0,expanded,0,old.Length);
   // Connect(Socket) is the pre-receive boundary, so the receive args are idle.
   // Keep the async socket buffer and the frame decoder's buffer synchronized.
   foreach(var field in typeof(BaseClient).GetFields(Fields).Where(f=>f.FieldType==typeof(SocketAsyncEventArgs))){var args=field.GetValue(client) as SocketAsyncEventArgs;if(args!=null&&Object.ReferenceEquals(args.Buffer,old))args.SetBuffer(expanded,0,expanded.Length);}
   ReadBuffer.SetValue(client,expanded);
   Console.WriteLine("BATTLE RECEIVE BUFFER: "+name+" "+old.Length+" -> "+expanded.Length);
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static bool Connect(BaseClient client,Socket socket){Prepare(client);return Original(client,socket);}
  // Outgoing battle links use BaseConnector.Connect(), not BaseClient.Connect(Socket).
  // Resize before connecting, including reconnects, while no receive is pending.
  [MethodImpl(MethodImplOptions.NoInlining)]static bool ConnectOutgoing(BaseConnector client){Prepare(client);return OriginalOutgoing(client);}
 }
}
