using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ActualLab.Interception;
using BenchmarkDotNet.Attributes;
using Castle.DynamicProxy;
using CastleIInvocation = Castle.DynamicProxy.IInvocation;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

// BenchmarkDotNet port of ActualLab.Tests' ProxyBenchmarkTest: measures the per-call cost of
// ActualLab's interceptors vs Castle DynamicProxy, for a "simple" interceptor (returns the method's
// default result) and a "pass-through" one (no handler; the call falls through to the target).
[MemoryDiagnoser]
public class ProxyInterceptionBenchmarks
{
    private const int OperationCount = 65_536;

    [Params("No proxy", "ActualLab", "ActualLab pass-through", "ActualLab no-handler",
        "Castle", "Castle pass-through")]
    public string Variant { get; set; } = "No proxy";

    private IProxyBenchmarkTester _proxy = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var target = new ProxyBenchmarkTester();
        _proxy = Variant switch {
            "No proxy" => target,
            "ActualLab" => (IProxyBenchmarkTester)Proxies.New(typeof(IProxyBenchmarkTester),
                new DefaultResultInterceptor(DefaultResultInterceptor.Options.Default, services)),
            "ActualLab pass-through" => (IProxyBenchmarkTester)Proxies.New(typeof(IProxyBenchmarkTester),
                new PassThroughInterceptor(PassThroughInterceptor.Options.Default, services), target),
            "ActualLab no-handler" => (IProxyBenchmarkTester)Proxies.New(typeof(IProxyBenchmarkTester),
                new NoHandlerInterceptor(NoHandlerInterceptor.Options.Default, services), target),
            "Castle" => (IProxyBenchmarkTester)new ProxyGenerator().CreateInterfaceProxyWithoutTarget(
                typeof(IProxyBenchmarkTester), new CastleDefaultResultInterceptor()),
            "Castle pass-through" => (IProxyBenchmarkTester)new ProxyGenerator().CreateInterfaceProxyWithTarget(
                typeof(IProxyBenchmarkTester), target, new CastlePassThroughInterceptor()),
            _ => throw new ArgumentOutOfRangeException(nameof(Variant)),
        };
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public void Void()
    {
        var proxy = _proxy;
        for (var i = 0; i < OperationCount; i++)
            proxy.Void();
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public int Int()
    {
        var proxy = _proxy;
        var result = 0;
        for (var i = 0; i < OperationCount; i++)
            result = proxy.Int(i);
        return result;
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public Task<int> IntTask()
    {
        var proxy = _proxy;
        var ct = CancellationToken.None;
        Task<int> result = null!;
        for (var i = 0; i < OperationCount; i++)
            result = proxy.IntTask(i, ct);
        return result;
    }
}

public interface IProxyBenchmarkTester : IRequiresFullProxy
{
    void Void();
    int Int(int a);
    Task<int> IntTask(int a, CancellationToken cancellationToken);
}

public sealed class ProxyBenchmarkTester : IProxyBenchmarkTester
{
    private static readonly Task<int> IntTaskResult = Task.FromResult(0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Void() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Int(int a) => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<int> IntTask(int a, CancellationToken cancellationToken) => IntTaskResult;
}

public sealed class DefaultResultInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public DefaultResultInterceptor(Options settings, IServiceProvider services) : base(settings, services)
    {
        MustInterceptSyncCalls = true;
        MustInterceptAsyncCalls = true;
    }

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var defaultResult = methodDef.DefaultResult;
        return _ => defaultResult;
    }
}

public sealed class PassThroughInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public PassThroughInterceptor(Options settings, IServiceProvider services) : base(settings, services)
    {
        MustInterceptSyncCalls = false;
        MustInterceptAsyncCalls = false;
    }

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
        => _ => throw new InvalidOperationException("Should never get to this point.");
}

// Interception is ENABLED (a MethodDef is created and the interceptor is engaged for every call), but
// CreateTypedHandler returns null. The binding then resolves the slot to NoHandler, which forwards the
// call to the target - so this measures full interception dispatch that ends in a fall-through, unlike
// PassThroughInterceptor which disables interception entirely.
public sealed class NoHandlerInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public NoHandlerInterceptor(Options settings, IServiceProvider services) : base(settings, services)
    {
        MustInterceptSyncCalls = true;
        MustInterceptAsyncCalls = true;
    }

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
        => null;
}

public class CastleDefaultResultInterceptor : IInterceptor
{
    private static readonly object VoidTag = new();
    private static readonly ConcurrentDictionary<Type, object?> ResultCache = new();

    [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "We assume test code is fully preserved")]
    public void Intercept(CastleIInvocation invocation)
    {
        var result = ResultCache.GetOrAdd(invocation.Method.ReturnType, static t => {
            if (!t.IsClass)
                return t == typeof(void) ? VoidTag : Activator.CreateInstance(t);

            return typeof(Task).IsAssignableFrom(t)
                ? t.IsGenericType
                    ? TaskExt.FromDefaultResult(t.GenericTypeArguments[0])
                    : Task.CompletedTask
                : null;
        });
        if (!ReferenceEquals(result, VoidTag))
            invocation.ReturnValue = result;
    }
}

public class CastlePassThroughInterceptor : IInterceptor
{
    public void Intercept(CastleIInvocation invocation)
        => invocation.Proceed();
}
