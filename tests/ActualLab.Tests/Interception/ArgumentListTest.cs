using System.Reflection;
using ActualLab.Interception;
using ActualLab.Reflection;

namespace ActualLab.Tests.Interception;

public class ArgumentListTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        for (var length = 0; length < ArgumentList.NativeTypeCount * 3; length++) {
            var itemTypes = Enumerable.Range(0, length).Select(_ => typeof(int)).ToArray();
            var list = (ArgumentList)ArgumentList.GetListType(itemTypes).CreateInstance();
            Test(length, list);
        }

        void Test(int length, ArgumentList l0)
        {
            var cts = new CancellationTokenSource();
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

            for (var i = 0; i <= length; i++) {
                // Insert test
                var l2 = l1.Insert(i, "s");
                l2.Length.Should().Be(length + 1);
                l2.Get<string>(i).Should().Be("s");
                Out.WriteLine(l2.ToString());
                var l3 = l2.Remove(i);
                l3.Should().Be(l1);

                // InsertCancellationToken test
                l2 = l1.InsertCancellationToken(i, cts.Token);
                l2.Length.Should().Be(length + 1);
                l2.Get<CancellationToken>(i).Should().Be(cts.Token);
                l3 = l2.Remove(i);
                l3.Should().Be(l1);
            }

            var method = GetType().GetMethod($"Format{length}", BindingFlags.Static | BindingFlags.NonPublic);
            if (method != null) {
                var invoker = l1.GetInvoker(method);
                var result = (string)invoker.Invoke(null, l1)!;
                Out.WriteLine($"Invoker result: {result}");
                result.Should().Be(l1.ToString());
            }
        }
    }

    [Fact]
    public void InvokerTest()
    {
        var type = GetType();
        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        var l = ArgumentList.Empty;
        var r = l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!).Invoke(this, l);
        r.Should().BeNull();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod1), bindingFlags)!));

        l = ArgumentList.New(1);
        r = l.GetInvoker(type.GetMethod(nameof(VoidMethod1), bindingFlags)!).Invoke(this, l);
        r.Should().BeNull();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!));

        r = l.GetInvoker(type.GetMethod(nameof(TaskMethod1), bindingFlags)!).Invoke(this, l);
        r.Should().BeOfType<Task<int>>().Which.Result.Should().Be(1);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(VoidMethod0), bindingFlags)!));

        l = ArgumentList.New(2, 3);
        r = l.GetInvoker(type.GetMethod(nameof(ValueTaskMethod2), bindingFlags)!).Invoke(this, l);
        r.Should().BeOfType<ValueTask<int>>().Which.AsTask().Result.Should().Be(6);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => l.GetInvoker(type.GetMethod(nameof(TaskMethod1), bindingFlags)!));
    }

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
        Out.WriteLine(nameof(VoidMethod0));
    }

    private void VoidMethod1(int x)
    {
        Out.WriteLine(nameof(VoidMethod1));
    }

    private Task<int> TaskMethod1(int x)
    {
        Out.WriteLine($"{nameof(TaskMethod1)}({x})");
        return Task.FromResult(x);
    }

    private ValueTask<int> ValueTaskMethod2(int x, int y)
    {
        Out.WriteLine($"{nameof(ValueTaskMethod2)}({x}, {y})");
        return new ValueTask<int>(x * y);
    }
}
