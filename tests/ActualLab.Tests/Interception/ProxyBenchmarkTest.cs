using ActualLab.Interception;
using ActualLab.Tests.Interception.Interceptors;
using Castle.DynamicProxy;

namespace ActualLab.Tests.Interception;

public class ProxyBenchmarkTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int DefaultIterationCount = 5_000_000;
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton(_ => DefaultResultInterceptor.Options.Default)
        .AddSingleton<DefaultResultInterceptor>()
        .AddSingleton(_ => PassThroughInterceptor.Options.Default)
        .AddSingleton<PassThroughInterceptor>()
        .AddSingleton(_ => new ProxyGenerator())
        .AddSingleton(_ => new CastleDefaultResultInterceptor())
        .AddSingleton(_ => new CastlePassThroughInterceptor())
        .BuildServiceProvider();

    [Fact]
    public async Task ActualLabProxyTest()
    {
        var noProxy = new ProxyProxyBenchmarkTester();
        var defaultResultInterceptor = Services.GetRequiredService<DefaultResultInterceptor>();
        var simpleProxy = (IProxyBenchmarkTester)Proxies.New(typeof(IProxyBenchmarkTester), defaultResultInterceptor);
        var passThroughInterceptor = Services.GetRequiredService<PassThroughInterceptor>();
        var passThroughProxy = (IProxyBenchmarkTester)Proxies.New(typeof(IProxyBenchmarkTester), passThroughInterceptor, noProxy);
        var castleGenerator = Services.GetRequiredService<ProxyGenerator>();
        var castleDefaultResultInterceptor = Services.GetRequiredService<CastleDefaultResultInterceptor>();
        var castleSimpleProxy = (IProxyBenchmarkTester)castleGenerator.CreateInterfaceProxyWithoutTarget(
            typeof(IProxyBenchmarkTester), castleDefaultResultInterceptor);
        var castlePassThroughInterceptor = Services.GetRequiredService<CastlePassThroughInterceptor>();
        var castlePassThroughProxy = (IProxyBenchmarkTester)castleGenerator.CreateInterfaceProxyWithTarget(
            typeof(IProxyBenchmarkTester), noProxy, castlePassThroughInterceptor);

        var variants = new[] {
            new ProxyVariant(noProxy, "No proxy"),
            new ProxyVariant(simpleProxy, "ActualLab interceptor"),
            new ProxyVariant(passThroughProxy, "ActualLab pass-through"),
            new ProxyVariant(castleSimpleProxy, "Castle interceptor"),
            new ProxyVariant(castlePassThroughProxy, "Castle pass-through"),
        };

        var iterationCount = DefaultIterationCount;
        await Benchmark("proxy.Void()", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            for (var i = 0; i < n; i++)
                proxy.Void();
        }, variants);
        await Benchmark("proxy.Task(ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = Task.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.Task(ct);
            Use(result);
        }, variants);
        await Benchmark("proxy.ValueTask(ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = ValueTask.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.ValueTask(ct);
            Use(result);
        }, variants);

        await Benchmark("proxy.Int()", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            int result = 0;
            for (var i = 0; i < n; i++)
                result = proxy.Int();
            Use(result);
        }, variants);
        await Benchmark("proxy.Int(i)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            int result = 0;
            for (var i = 0; i < n; i++)
                result = proxy.Int(i);
            Use(result);
        }, variants);
        await Benchmark("proxy.Int(i, i)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            int result = 0;
            for (var i = 0; i < n; i++)
                result = proxy.Int(i, i);
            Use(result);
        }, variants);
        await Benchmark("proxy.IntFromObj(obj, obj)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            int result = 0;
            for (var i = 0; i < n; i++)
                result = proxy.IntFromObj(proxy, proxy);
            Use(result);
        }, variants);

        await Benchmark("proxy.IntTask(ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = Task.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.IntTask(ct);
            Use(result);
        }, variants);
        await Benchmark("proxy.IntTask(i, ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = Task.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.IntTask(i, ct);
            Use(result);
        }, variants);
        await Benchmark("proxy.IntTask(i, i, ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = Task.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.IntTask(i, i, ct);
            Use(result);
        }, variants);

        await Benchmark("proxy.IntValueTask(ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = ValueTask.FromResult(0);
            for (var i = 0; i < n; i++)
                result = proxy.IntValueTask(ct);
            Use(result);
        }, variants);
        await Benchmark("proxy.IntValueTask(i, ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = ValueTask.FromResult(0);
            for (var i = 0; i < n; i++)
                result = proxy.IntValueTask(i, ct);
            Use(result);
        }, variants);
        await Benchmark("proxy.IntValueTask(i, i, ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = ValueTask.FromResult(0);
            for (var i = 0; i < n; i++)
                result = proxy.IntValueTask(i, i, ct);
            Use(result);
        }, variants);

        await Benchmark("proxy.CommandLike(obj, ct)", iterationCount, (v, n) => {
            var proxy = v.Proxy;
            var ct = CancellationToken.None;
            var result = Task.CompletedTask;
            for (var i = 0; i < n; i++)
                result = proxy.CommandLike(proxy, ct);
            Use(result);
        }, variants);
    }

    // Private methods

    private static void Use<T>(T obj)
        => _ = obj?.GetHashCode();

    // Nested types

    private sealed record ProxyVariant(IProxyBenchmarkTester Proxy, string Name)
    {
        public override string ToString() => Name;
    }
}
