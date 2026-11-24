using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Local36 {
 // Native trampolines retain the actual protected JIT implementation. A Harmony
 // IL wrapper would read this package's placeholder body and lose that code.
 public sealed class NativeMethodHook36:IDisposable {
  readonly object handle;readonly Type nativeInterface,baseInterface;readonly MethodInfo source;
  static IntPtr Body(MethodInfo method){RuntimeHelpers.PrepareMethod(method.MethodHandle);IntPtr p=method.MethodHandle.GetFunctionPointer();for(int i=0;i<4;i++){if(Marshal.ReadByte(p)==0xe9){p=IntPtr.Add(p,5+Marshal.ReadInt32(p,1));continue;}if(Marshal.ReadByte(p)==0xff&&Marshal.ReadByte(p,1)==0x25){p=Marshal.ReadIntPtr(IntPtr.Add(p,6+Marshal.ReadInt32(p,2)));continue;}break;}return p;}
  public NativeMethodHook36(MethodInfo from,MethodInfo to){
   if(from==null||to==null||IntPtr.Size!=8)throw new ArgumentException("Verified x64 native methods required");source=from;
   var assembly=typeof(HarmonyLib.Harmony).Assembly;var factoryType=assembly.GetType("MonoMod.Core.DetourFactory",true);var requestType=assembly.GetType("MonoMod.Core.CreateNativeDetourRequest",true);
   nativeInterface=assembly.GetType("MonoMod.Core.ICoreNativeDetour",true);baseInterface=assembly.GetType("MonoMod.Core.ICoreDetourBase",true);
   object factory=factoryType.GetProperty("Current").GetValue(null,null),request=Activator.CreateInstance(requestType);
   requestType.GetProperty("Source").SetValue(request,Body(from),null);requestType.GetProperty("Target").SetValue(request,Body(to),null);requestType.GetProperty("ApplyByDefault").SetValue(request,false,null);
   handle=assembly.GetType("MonoMod.Core.IDetourFactory",true).GetMethod("CreateNativeDetour").Invoke(factory,new[]{request});
  }
  public void Apply(){baseInterface.GetMethod("Apply").Invoke(handle,null);}
  public T Original<T>() where T:class {
   IntPtr pointer=(IntPtr)nativeInterface.GetProperty("OrigEntrypoint").GetValue(handle,null);if(pointer==IntPtr.Zero)throw new InvalidOperationException("Original native entrypoint is unavailable");
   var invoke=typeof(T).GetMethod("Invoke");var args=Array.ConvertAll(invoke.GetParameters(),p=>p.ParameterType);var method=new DynamicMethod("native_original_"+source.Name,invoke.ReturnType,args,typeof(NativeMethodHook36),true);var il=method.GetILGenerator();for(int i=0;i<args.Length;i++)il.Emit(OpCodes.Ldarg,(short)i);il.Emit(OpCodes.Ldc_I8,pointer.ToInt64());il.Emit(OpCodes.Conv_I);il.EmitCalli(OpCodes.Calli,CallingConventions.Standard,invoke.ReturnType,args,null);il.Emit(OpCodes.Ret);return method.CreateDelegate(typeof(T)) as T;
  }
  public void Dispose(){var disposable=handle as IDisposable;if(disposable!=null)disposable.Dispose();}
 }
}
