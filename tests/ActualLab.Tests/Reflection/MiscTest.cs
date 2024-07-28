using System.Reflection;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

public class MiscTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void GetInvokerTest()
    {
        Out.WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");
        var l0 = ArgumentList.New();
        var l2 = ArgumentList.New(1, "s");
        var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
        var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
        l0.GetInvoker(m0).Invoke(null, l0).Should().Be("");
        l2.GetInvoker(m2).Invoke(null, l2).Should().Be("1, s");
    }

    [Fact]
    public void CreateInstanceTest()
    {
        for (var i = 0; i < ArgumentList.Types.Length; i++) {
            var tArguments = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();
            var t = ArgumentList.FindType(tArguments);
            var l = (ArgumentList)t.CreateInstance();
            l.Length.Should().Be(i);
        }
    }

    // Nested types

    public static class Invoker
    {
        public static string Format0()
            => "";
        public static string Format2(int a1, string a2)
            => $"{a1}, {a2}";
    }
}
