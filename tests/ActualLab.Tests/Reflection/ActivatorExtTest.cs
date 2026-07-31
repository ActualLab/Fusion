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

    [Fact]
    public void RegisterConstructorDelegateTest()
    {
        // arrange
        Func<P0> f0 = static () => new P0();
        Func<int, P1> f1 = static a => new P1(a);
        Func<int, bool, P2> f2 = static (a, b) => new P2(a, b);
        Func<int, bool, Unit, P3> f3 = static (a, b, c) => new P3(a, b, c);
        Func<int, bool, Unit, string, P4> f4 = static (a, b, c, d) => new P4(a, b, c, d);
        Func<int, bool, Unit, string, double, P5> f5 = static (a, b, c, d, e) => new P5(a, b, c, d, e);
        ActivatorExt.RegisterConstructorDelegate(f0);
        ActivatorExt.RegisterConstructorDelegate(f1);
        ActivatorExt.RegisterConstructorDelegate(f2);
        ActivatorExt.RegisterConstructorDelegate(f3);
        ActivatorExt.RegisterConstructorDelegate(f4);
        ActivatorExt.RegisterConstructorDelegate(f5);

        // act & assert - BeSameAs proves the registered delegate is used instead of codegen
        typeof(P0).GetConstructorDelegate().Should().BeSameAs(f0);
        typeof(P1).GetConstructorDelegate(typeof(int)).Should().BeSameAs(f1);
        typeof(P2).GetConstructorDelegate(typeof(int), typeof(bool)).Should().BeSameAs(f2);
        typeof(P3).GetConstructorDelegate(typeof(int), typeof(bool), typeof(Unit)).Should().BeSameAs(f3);
        typeof(P4).GetConstructorDelegate(typeof(int), typeof(bool), typeof(Unit), typeof(string))
            .Should().BeSameAs(f4);
        typeof(P5).GetConstructorDelegate(typeof(int), typeof(bool), typeof(Unit), typeof(string), typeof(double))
            .Should().BeSameAs(f5);

        typeof(P0).CreateInstance().Should().Be(new P0());
        typeof(P1).CreateInstance(1).Should().Be(new P1(1));
        typeof(P2).CreateInstance(1, false).Should().Be(new P2(1, false));
        typeof(P3).CreateInstance(1, false, default(Unit)).Should().Be(new P3(1, false, default));
        typeof(P4).CreateInstance(1, false, default(Unit), "1").Should().Be(new P4(1, false, default, "1"));
        typeof(P5).CreateInstance(1, false, default(Unit), "1", 1.0d)
            .Should().Be(new P5(1, false, default, "1", 1.0d));
    }

    [Fact]
    public void RegisteredConstructorDelegateIsCovariantTest()
    {
        // Registration must use the constructor's exact declaring type, because callers such as
        // RpcInboundCall.GetFactory cast the cached delegate to a Func returning a base type.
        Func<int, PDerived> factory = static a => new PDerived(a);
        ActivatorExt.RegisterConstructorDelegate(factory);

        var baseFactory = (Func<int, PBase>)typeof(PDerived).GetConstructorDelegate(typeof(int))!;
        baseFactory.Invoke(1).Should().Be(new PDerived(1));
    }

    [Fact]
    public void OnCreateDelegateFiresOnCodegenTest()
    {
        // arrange
        var created = new ConcurrentBag<Type>();
        var oldOnCreateDelegate = RuntimeCodegen.OnCreateDelegate;
        RuntimeCodegen.OnCreateDelegate = (member, _) => created.Add(member.DeclaringType!);
        try {
            // act
            typeof(H1).GetConstructorDelegate(typeof(int));
            ActivatorExt.RegisterConstructorDelegate(static (int a) => new H2(a));
            typeof(H2).GetConstructorDelegate(typeof(int));
        }
        finally {
            RuntimeCodegen.OnCreateDelegate = oldOnCreateDelegate;
        }

        // assert
        created.Should().Contain(typeof(H1));
        created.Should().NotContain(typeof(H2));
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

    public record P0();
    public record P1(int A);
    public record P2(int A, bool B);
    public record P3(int A, bool B, Unit C);
    public record P4(int A, bool B, Unit C, string D);
    public record P5(int A, bool B, Unit C, string D, double E);

    public record PBase(int A);
    public sealed record PDerived(int A) : PBase(A);

    public record H1(int A);
    public record H2(int A);
}
