using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Local36 {
 // Compact IL disassembler used to read the native 3.6 methods from inside the
 // running server (the assemblies are VM-protected on disk but fully decoded in
 // memory). Used for diagnosis only; writes a readable listing to a log file.
 public static class IlDump36 {
  static readonly OpCode[] OneByte=new OpCode[256];
  static readonly OpCode[] TwoByte=new OpCode[256];
  static IlDump36(){
   foreach(FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){
    OpCode op=(OpCode)f.GetValue(null);
    if(op.Size==1)OneByte[op.Value]=op;
    else TwoByte[op.Value&0xff]=op;
   }
  }
  static string Fmt(object o){return o==null?"null":o.ToString();}
  public static string Dump(MethodInfo m){
   if(m==null)return "method is null";
   var sb=new StringBuilder();
   sb.AppendLine("=== "+m.DeclaringType.FullName+" :: "+m.Name+" "+(m.IsVirtual?"virtual ":"")+"(return "+m.ReturnType.Name+") ===");
   try{
    MethodBody body=m.GetMethodBody();
    if(body==null){sb.AppendLine("(no body)");return sb.ToString();}
    byte[] il=body.GetILAsByteArray();
    Module mod=m.Module;
    Type[] genArgs=m.IsGenericMethodDefinition?m.GetGenericArguments():null;
    Type[] typeArgs=m.DeclaringType.IsGenericType?m.DeclaringType.GetGenericArguments():null;
    int off=0;
    while(off<il.Length){
     int start=off;
     OpCode op;
     int size;
     if(il[off]==0xfe){op=TwoByte[il[off+1]];size=2;}
     else{op=OneByte[il[off]];size=1;}
     off+=size;
     object operand=null;string operandStr="";
     try{
      switch(op.OperandType){
       case OperandType.InlineNone:break;
       case OperandType.ShortInlineI:operand=(sbyte)il[off];operandStr=operand.ToString();off+=1;break;
       case OperandType.InlineI:operand=BitConverter.ToInt32(il,off);operandStr=operand.ToString();off+=4;break;
       case OperandType.InlineI8:operand=BitConverter.ToInt64(il,off);operandStr=operand.ToString();off+=8;break;
       case OperandType.ShortInlineR:operand=BitConverter.ToSingle(il,off);operandStr=operand.ToString();off+=4;break;
       case OperandType.InlineR:operand=BitConverter.ToDouble(il,off);operandStr=operand.ToString();off+=8;break;
       case OperandType.ShortInlineVar:operand=il[off];operandStr=operand.ToString();off+=1;break;
       case OperandType.InlineVar:operand=BitConverter.ToUInt16(il,off);operandStr=operand.ToString();off+=2;break;
       case OperandType.ShortInlineBrTarget:operand=(sbyte)il[off];operandStr=operand.ToString();off+=1;break;
       case OperandType.InlineBrTarget:operand=BitConverter.ToInt32(il,off);operandStr=operand.ToString();off+=4;break;
       case OperandType.InlineString:case OperandType.InlineMethod:case OperandType.InlineField:case OperandType.InlineType:case OperandType.InlineTok:case OperandType.InlineSig:{
        int token=BitConverter.ToInt32(il,off);off+=4;
        try{operand=op.OperandType==OperandType.InlineString?(object)mod.ResolveString(token):(object)mod.ResolveMember(token,typeArgs,genArgs);}catch{operand=("0x"+token.ToString("x8"));}
        if(operand is MemberInfo){var mi=(MemberInfo)operand;operandStr=(mi.DeclaringType==null?"":mi.DeclaringType.Name)+"."+mi.Name;}
        else if(operand is string)operandStr="\""+operand+"\"";
        else operandStr=operand==null?"null":operand.ToString();
        break;}
       case OperandType.InlineSwitch:{
        int n=BitConverter.ToInt32(il,off);off+=4;var sb2=new StringBuilder("switch["+n+"] ");for(int i=0;i<n;i++){sb2.Append(BitConverter.ToInt32(il,off)).Append(' ');off+=4;}operandStr=sb2.ToString();break;}
       default:operandStr="?";break;
      }
     }catch{operandStr="(decode error)";}
     sb.Append("IL_").Append(start.ToString("x4")).Append(": ").Append(op.Name);
     if(operandStr.Length>0)sb.Append(" ").Append(operandStr);
     sb.AppendLine();
    }
   }catch(Exception e){sb.AppendLine("(il dump failed: "+e.Message+")");}
   return sb.ToString();
  }
  public static string DumpMembers(Type t){
   var sb=new StringBuilder();
   sb.AppendLine("=== fields of "+t.FullName+" ===");
   foreach(FieldInfo f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
    sb.AppendLine("  field "+f.FieldType.Name+" "+f.Name);
   sb.AppendLine("=== properties of "+t.FullName+" ===");
   foreach(PropertyInfo p in t.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
    sb.AppendLine("  prop "+p.PropertyType.Name+" "+p.Name);
   return sb.ToString();
  }
 }
}
