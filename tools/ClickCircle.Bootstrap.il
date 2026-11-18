.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89)
  .ver 4:0:0:0
}
.assembly ClickCircle.Bootstrap
{
  .ver 1:0:0:0
}
.module ClickCircle.pvp
.corflags 0x00000000
.imagebase 0x180000000
.stackreserve 0x00100000
.vtfixup [1] int64 fromunmanaged at VT_01
.data VT_01 = int64(0)

.class public abstract sealed ClickCircleBootstrap extends [mscorlib]System.Object
{
  .method public hidebysig static int32 modopt([mscorlib]System.Runtime.CompilerServices.CallConvStdcall)
    PXCE_GetPlugin(uint32 apiVersion, native int reserved, native int outputInterface) cil managed
  {
    .vtentry 1:1
    .export [1] as PXCE_GetPlugin
    .maxstack 5
    .locals init ([0] object[] arguments, [1] int32 result, [2] class [mscorlib]System.Exception exception)
    .try
    {
      ldc.i4.3
      newarr [mscorlib]System.Object
      stloc.0
      ldloc.0
      ldc.i4.0
      ldarg.0
      box [mscorlib]System.UInt32
      stelem.ref
      ldloc.0
      ldc.i4.1
      ldarg.1
      box [mscorlib]System.IntPtr
      stelem.ref
      ldloc.0
      ldc.i4.2
      ldarg.2
      box [mscorlib]System.IntPtr
      stelem.ref
      call class [mscorlib]System.Reflection.Assembly [mscorlib]System.Reflection.Assembly::GetExecutingAssembly()
      callvirt instance string [mscorlib]System.Reflection.Assembly::get_Location()
      call string [mscorlib]System.IO.Path::GetDirectoryName(string)
      ldstr "ClickCircle.Core.dll"
      call string [mscorlib]System.IO.Path::Combine(string,string)
      call class [mscorlib]System.Reflection.Assembly [mscorlib]System.Reflection.Assembly::LoadFrom(string)
      ldstr "ClickCircle.Plugin"
      callvirt instance class [mscorlib]System.Type [mscorlib]System.Reflection.Assembly::GetType(string)
      ldstr "Create"
      callvirt instance class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string)
      ldnull
      ldloc.0
      callvirt instance object [mscorlib]System.Reflection.MethodBase::Invoke(object,object[])
      unbox.any [mscorlib]System.Int32
      stloc.1
      leave Return
    }
    catch [mscorlib]System.Exception
    {
      stloc.2
      ldloc.2
      call int32 [mscorlib]System.Runtime.InteropServices.Marshal::GetHRForException(class [mscorlib]System.Exception)
      stloc.1
      leave Return
    }
  Return:
    ldloc.1
    ret
  }
}
