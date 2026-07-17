using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class SlotTestBase : IRequiresAsyncProxy
{
    public virtual Task<int> A(int x) => Task.FromResult(x);
    public virtual Task<int> B() => Task.FromResult(1);
}

public class SlotTestService : SlotTestBase
{
    public override Task<int> B() => Task.FromResult(2);
    public virtual Task<string> C(string s, CancellationToken cancellationToken) => Task.FromResult(s);
}

public interface ISlotLeft : IRequiresAsyncProxy
{
    Task Run();
    Task Select(int value);
}

public interface ISlotRight : IRequiresAsyncProxy
{
    Task Run();
    Task Select(string value);
}

public interface ISlotDiamond : ISlotLeft, ISlotRight;

public interface ISlotGeneric<T> : IRequiresAsyncProxy
{
    Task<T> Get(T value);
}

public sealed class CapturingInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public volatile Invocation[] Invocations = [];
    public int HandlerCreationCount;

    public CapturingInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    { }

    public Invocation LastInvocation => Invocations[^1];

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        Interlocked.Increment(ref HandlerCreationCount);
        var defaultResult = methodDef.DefaultResult;
        return invocation => {
            lock (this)
                Invocations = [..Invocations, invocation];
            return defaultResult;
        };
    }
}

public sealed class SelectCountingInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public int SelectHandlerCallCount;

    public SelectCountingInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    { }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
    {
        Interlocked.Increment(ref SelectHandlerCallCount);
        return CreateHandler(invocation);
    }

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var defaultResult = methodDef.DefaultResult;
        return _ => defaultResult;
    }
}

public class ProxyMethodTableTest
{
    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task InvocationMustCarryMethodTableAndIndex()
    {
        var interceptor = NewCapturingInterceptor();
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        await proxy.A(10);
        var invocation = interceptor.LastInvocation;
        invocation.MethodIndex.Should().BeGreaterThanOrEqualTo(0);
        var table = invocation.MethodTable!;
        table.Should().NotBeNull();
        table.ProxyType.Should().Be(proxy.GetType());
        table.ServiceType.Should().Be(typeof(SlotTestService));
        invocation.Method.Name.Should().Be(nameof(SlotTestService.A));
        table.Methods[invocation.MethodIndex].Should().BeSameAs(invocation.Method);
        table.GetIndex(invocation.Method).Should().Be(invocation.MethodIndex);
        invocation.MethodRef.Should().Be(new ProxyMethodRef(table, invocation.MethodIndex));

        await proxy.C("x", default);
        interceptor.LastInvocation.Method.Name.Should().Be(nameof(SlotTestService.C));
        interceptor.LastInvocation.MethodTable.Should().BeSameAs(table);
    }

    [Fact]
    public async Task OverriddenAndInheritedMethodsMustResolveToBaseDeclarationSlot()
    {
        var interceptor = NewCapturingInterceptor();
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        await proxy.B();
        var invocation = interceptor.LastInvocation;
        var table = invocation.MethodTable!;

        var baseB = typeof(SlotTestBase).GetMethod(nameof(SlotTestBase.B))!;
        table.GetIndex(baseB).Should().Be(invocation.MethodIndex);

        await proxy.A(1);
        invocation = interceptor.LastInvocation;
        var baseA = typeof(SlotTestBase).GetMethod(nameof(SlotTestBase.A))!;
        invocation.MethodTable.Should().BeSameAs(table);
        table.GetIndex(baseA).Should().Be(invocation.MethodIndex);

        table.GetIndex(typeof(object).GetMethod(nameof(GetHashCode))!).Should().Be(-1);
    }

    [Fact]
    public async Task DiamondInterfaceMethodsMustShareOneSlot()
    {
        var interceptor = NewCapturingInterceptor();
        var proxy = (ISlotDiamond)Proxies.New(typeof(ISlotDiamond), interceptor);

        await ((ISlotLeft)proxy).Run();
        var invocation = interceptor.LastInvocation;
        var table = invocation.MethodTable!;
        var runIndex = invocation.MethodIndex;

        await ((ISlotRight)proxy).Run();
        interceptor.LastInvocation.MethodIndex.Should().Be(runIndex);

        var leftRun = typeof(ISlotLeft).GetMethod(nameof(ISlotLeft.Run))!;
        var rightRun = typeof(ISlotRight).GetMethod(nameof(ISlotRight.Run))!;
        table.GetIndex(leftRun).Should().Be(runIndex);
        table.GetIndex(rightRun).Should().Be(runIndex);

        await proxy.Select(1);
        var intSelectIndex = interceptor.LastInvocation.MethodIndex;
        await proxy.Select("s");
        var stringSelectIndex = interceptor.LastInvocation.MethodIndex;
        intSelectIndex.Should().NotBe(stringSelectIndex);
    }

    [Fact]
    public async Task ClosedGenericProxyTypesMustHaveDistinctTables()
    {
        var interceptor = NewCapturingInterceptor();
        var intProxy = (ISlotGeneric<int>)Proxies.New(typeof(ISlotGeneric<int>), interceptor);
        var stringProxy = (ISlotGeneric<string>)Proxies.New(typeof(ISlotGeneric<string>), interceptor);

        await intProxy.Get(1);
        var intTable = interceptor.LastInvocation.MethodTable!;
        await stringProxy.Get("s");
        var stringTable = interceptor.LastInvocation.MethodTable!;

        stringTable.Should().NotBeSameAs(intTable);
        intTable.Methods.Single().ReturnType.Should().Be(typeof(Task<int>));
        stringTable.Methods.Single().ReturnType.Should().Be(typeof(Task<string>));
    }

    [Fact]
    public async Task InterceptorMustBindJustOnce()
    {
        var interceptor1 = NewCapturingInterceptor();
        var interceptor2 = NewCapturingInterceptor();
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor1);

        await proxy.A(1);
        interceptor1.Invocations.Should().HaveCount(1);
        ((IProxy)proxy).Binding.Interceptor.Should().BeSameAs(interceptor1);
        ((IProxy)proxy).MethodTable.Should().BeSameAs(interceptor1.LastInvocation.MethodTable);

        var rebind = () => interceptor2.BindTo(proxy);
        rebind.Should().Throw<InvalidOperationException>();

        await proxy.A(2);
        interceptor1.Invocations.Should().HaveCount(2);
        interceptor2.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task NoHandlerSlotMustInvokeOriginalImplementation()
    {
        var interceptor = new CapturingInterceptor(CapturingInterceptor.Options.Default, Services) {
            MustInterceptAsyncCalls = false,
        };
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        (await proxy.B()).Should().Be(2);
        (await proxy.B()).Should().Be(2);
        interceptor.HandlerCreationCount.Should().Be(0);
        interceptor.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectHandlerMustResolveOncePerSlot()
    {
        var interceptor = new SelectCountingInterceptor(SelectCountingInterceptor.Options.Default, Services);
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        await proxy.A(1);
        await proxy.A(2);
        await proxy.A(3);
        interceptor.SelectHandlerCallCount.Should().Be(1);

        await proxy.B();
        interceptor.SelectHandlerCallCount.Should().Be(2);
    }

    [Fact]
    public async Task WithMustPreserveSlot()
    {
        var interceptor = NewCapturingInterceptor();
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        await proxy.A(1);
        var invocation = interceptor.LastInvocation;
        var changed = invocation.With(ArgumentList.New(2, CancellationToken.None));
        changed.MethodIndex.Should().Be(invocation.MethodIndex);
        changed.MethodTable.Should().BeSameAs(invocation.MethodTable);
        changed.Method.Should().BeSameAs(invocation.Method);
    }

    [Fact]
    public async Task ConcurrentFirstCallsMustPublishOneSlotState()
    {
        var interceptor = new SelectCountingInterceptor(SelectCountingInterceptor.Options.Default, Services);
        var proxy = (SlotTestService)Proxies.New(typeof(SlotTestService), interceptor);

        var tasks = Enumerable.Range(0, 32)
            .Select(i => Task.Run(() => proxy.A(i)))
            .ToArray();
        await Task.WhenAll(tasks);

        // Concurrent first calls may resolve duplicate candidates, but once
        // a slot state is published, no further resolution can happen
        var callCountAfterBurst = interceptor.SelectHandlerCallCount;
        await proxy.A(100);
        await proxy.A(101);
        interceptor.SelectHandlerCallCount.Should().Be(callCountAfterBurst);
    }

    // Private methods

    private static CapturingInterceptor NewCapturingInterceptor()
        => new(CapturingInterceptor.Options.Default, Services);
}
