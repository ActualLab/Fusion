using System.Reflection;
using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class ArgumentListTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BasicTest(bool useGenerics)
    {
        for (var length = 0; length <= ArgumentList.MaxItemCount; length++) {
            var itemTypes = Enumerable.Range(0, length).Select(_ => typeof(int)).ToArray();
            var listDef = ArgumentListType.Get(useGenerics, itemTypes);
            WriteLine(listDef.ToString());
            var list = listDef.Factory.Invoke();
            Test(length, list);
        }

        void Test(int length, ArgumentList l0) {
            l0.Length.Should().Be(length);
            var l1 = l0.Duplicate();
            for (var i = 0; i < length; i++) {
                // Get, GetUntyped, Set, SetUntyped test
                l0.Get<int>(i).Should().Be(0);
                l0.GetUntyped(i).Should().Be(0);
                l1.Set(i, i);
                l1.Get<int>(i).Should().Be(i);
                l1.GetUntyped(i).Should().Be(i);
                l1.SetUntyped(i, i);
                l1.Get<int>(i).Should().Be(i);
            }

            var method = GetType().GetMethod($"Format{length}", BindingFlags.Static | BindingFlags.NonPublic);
            if (method is not null) {
                var invoker = l1.GetInvoker(method);
                var result = (string)invoker.Invoke(null, l1)!;
                WriteLine($"Invoker result: {result}");
                result.Should().Be(l1.ToString());
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvokerTest1(bool useGenerics)
    {
        var type = GetType();
        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        var l = ArgumentList.Empty;
        var r = l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!).Invoke(this, l);
        r.Should().BeNull();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod1), bindingFlags)!));

        l = ArgumentListType.Get(useGenerics, typeof(int)).Factory();
        l.Set(0, 1);
        r = l.GetInvoker(type.GetMethod(nameof(VoidMethod1), bindingFlags)!).Invoke(this, l);
        r.Should().BeNull();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!));

        r = l.GetInvoker(type.GetMethod(nameof(TaskMethod1), bindingFlags)!).Invoke(this, l);
        r.Should().BeOfType<Task<int>>().Which.Result.Should().Be(1);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!));

        l = ArgumentListType.Get(useGenerics, typeof(int), typeof(int)).Factory();
        l.Set(0, 2);
        l.Set(1, 3);
        r = l.GetInvoker(type.GetMethod(nameof(ValueTaskMethod2), bindingFlags)!).Invoke(this, l);
        r.Should().BeOfType<ValueTask<int>>().Which.AsTask().Result.Should().Be(6);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(TaskMethod1), bindingFlags)!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvokerTest2(bool useGenerics)
    {
        var l0 = ArgumentListType.Get(useGenerics).Factory();
        var l2 = ArgumentListType.Get(useGenerics, typeof(int), typeof(string)).Factory();
        l2.Set(0, 1);
        l2.Set(1, "s");
        var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
        var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
        l0.GetInvoker(m0).Invoke(null, l0).Should().Be("");
        l2.GetInvoker(m2).Invoke(null, l2).Should().Be("1, s");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateInstanceTest(bool useGenerics)
    {
        for (var i = 0; i <= ArgumentList.MaxItemCount; i++) {
            var itemTypes = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();

            var def = ArgumentListType.Get(useGenerics, itemTypes);
            def.ItemTypes.Should().Equal(itemTypes);

            var l = def.Factory.Invoke();
            l.Length.Should().Be(i);
            l.Type.Should().BeSameAs(def);
        }
    }

    // Private methods

    private static string Format0()
        => "()";
    private static string Format1(int i0)
        => $"({i0})";
    private static string Format2(int i0, int i1)
        => $"({i0}, {i1})";
    private static string Format3(int i0, int i1, int i2)
        => $"({i0}, {i1}, {i2})";
    private static string Format4(int i0, int i1, int i2, int i3)
        => $"({i0}, {i1}, {i2}, {i3})";
    private static string Format5(int i0, int i1, int i2, int i3, int i4)
        => $"({i0}, {i1}, {i2}, {i3}, {i4})";
    private static string Format6(int i0, int i1, int i2, int i3, int i4, int i5)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5})";
    private static string Format7(int i0, int i1, int i2, int i3, int i4, int i5, int i6)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6})";
    private static string Format8(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7})";
    private static string Format9(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8})";
    private static string Format10(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8}, {i9})";
    private static string Format11(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9, int i10)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8}, {i9}, {i10})";
    private static string Format12(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9, int i10, int i11)
        => $"({i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8}, {i9}, {i10}, {i11})";

    private void VoidMethod0()
    {
        WriteLine(nameof(VoidMethod0));
    }

    private void VoidMethod1(int x)
    {
        WriteLine(nameof(VoidMethod1));
    }

    private Task<int> TaskMethod1(int x)
    {
        WriteLine($"{nameof(TaskMethod1)}({x})");
        return Task.FromResult(x);
    }

    private ValueTask<int> ValueTaskMethod2(int x, int y)
    {
        WriteLine($"{nameof(ValueTaskMethod2)}({x}, {y})");
        return new ValueTask<int>(x * y);
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
