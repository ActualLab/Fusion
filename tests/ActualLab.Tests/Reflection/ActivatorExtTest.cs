using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

public class ActivatorExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    public class SimpleClass;

    [Fact]
    public void CreateInstanceTest()
    {
        typeof(R0).CreateInstance()
            .Should().BeOfType<R0>();
        typeof(R1).CreateInstance(1)
            .Should().BeOfType<R1>();
        typeof(R2).CreateInstance(1, false)
            .Should().BeOfType<R2>();
        typeof(R3).CreateInstance(1, false, default(Unit))
            .Should().BeOfType<R3>();
        typeof(R4).CreateInstance(1, false, default(Unit), "1")
            .Should().BeOfType<R4>();
        typeof(R5).CreateInstance(1, false, default(Unit), "1", 1.0d)
            .Should().BeOfType<R5>();
    }

    [Fact]
    public void NewTest()
    {
        ActivatorExt.New<int>().Should().Be(0);
        ActivatorExt.New<int>(false).Should().Be(0);
        ActivatorExt.New<Unit>().Should().Be(default(Unit));
        ActivatorExt.New<Unit>(false).Should().Be(default(Unit));
        ActivatorExt.New<SimpleClass>().Should().BeOfType(typeof(SimpleClass));
        ActivatorExt.New<SimpleClass>(false).Should().BeOfType(typeof(SimpleClass));

        ((Func<string>) (() => ActivatorExt.New<string>()))
            .Should().Throw<InvalidOperationException>();
        ActivatorExt.New<string>(false).Should().Be(null);
    }

    [Fact]
    public void CreateInstanceOfValueTypeTest()
    {
        // The constructor delegate returns the declaring type, and CreateInstance casts it to
        // Func<..., object> - delegate return types are covariant only for reference types, so a
        // struct has to be boxed inside the delegate rather than by the cast.
        typeof(S0).CreateInstance().Should().Be(new S0());
        typeof(S1).CreateInstance(1).Should().Be(new S1(1));
        typeof(S2).CreateInstance(1, false).Should().Be(new S2(1, false));
        typeof(S3).CreateInstance(1, false, default(Unit)).Should().Be(new S3(1, false, default));
        typeof(S4).CreateInstance(1, false, default(Unit), "1").Should().Be(new S4(1, false, default, "1"));
        typeof(S5).CreateInstance(1, false, default(Unit), "1", 1.0d)
            .Should().Be(new S5(1, false, default, "1", 1.0d));
    }

    // Nested types

    public readonly record struct S0(int Unused = 0);
    public readonly record struct S1(int A);
    public readonly record struct S2(int A, bool B);
    public readonly record struct S3(int A, bool B, Unit C);
    public readonly record struct S4(int A, bool B, Unit C, string D);
    public readonly record struct S5(int A, bool B, Unit C, string D, double E);

    public record R0();
    public record R1(int A);
    public record R2(int A, bool B);
    public record R3(int A, bool B, Unit C);
    public record R4(int A, bool B, Unit C, string D);
    public record R5(int A, bool B, Unit C, string D, double E);
}
