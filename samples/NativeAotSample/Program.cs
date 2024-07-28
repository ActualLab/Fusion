using System.Reflection;
using ActualLab.Interception;
using ActualLab.Reflection;
using static System.Console;

#pragma warning disable IL3050

WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");
var l0 = ArgumentList.New();
var l2 = ArgumentList.New(1, "s");
var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
WriteLine(l0.GetInvoker(m0).Invoke(null, l0));
WriteLine(l2.GetInvoker(m2).Invoke(null, l2));

for (var i = 0; i < ArgumentList.Types.Length; i++) {
    var tArguments = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();
    var t = ArgumentList.FindType(tArguments);
    var l = t.CreateInstance();
    WriteLine($"{l}, {FuncExt.GetFuncType(tArguments, typeof(object)).GetName()}, {FuncExt.GetActionType(tArguments).GetName()}");
}

public static class Invoker
{
    public static string Format0()
        => "Format0";
    public static string Format2(int a1, string a2)
        => $"Format2: {a1}, {a2}";
}
