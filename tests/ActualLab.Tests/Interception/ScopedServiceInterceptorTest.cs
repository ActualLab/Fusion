using ActualLab.Interception;
using ActualLab.Interception.Interceptors;
using ActualLab.Rpc;

namespace ActualLab.Tests.Interception;

public interface IScopedServiceTestService :
    IRpcService,
    IRequiresFullProxy // Required only for this specific test (it also checks sync method calls)
{
    void VoidMethod();
    int IntMethod();
    Task TaskMethod();
    Task<int> TaskIntMethod();
    ValueTask ValueTaskMethod();
    ValueTask<int> ValueTaskIntMethod();
}

public class ScopedServiceTestService(IServiceProvider services) : IScopedServiceTestService, IDisposable
{
    private int _callCount;

    public static ThreadSafe<bool> IsDisposeCheckFailed { get; } = new();

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _callCount) != -1)
            IsDisposeCheckFailed.Value = true;
    }

    public void VoidMethod()
        => services.IsScoped().Should().BeTrue();

    public int IntMethod()
    {
        services.IsScoped().Should().BeTrue();
        return 1;
    }

    public async Task TaskMethod()
    {
        services.IsScoped().Should().BeTrue();
        Interlocked.Increment(ref _callCount);
        await Task.Delay(200);
        Interlocked.Decrement(ref _callCount);
    }

    public async Task<int> TaskIntMethod()
    {
        services.IsScoped().Should().BeTrue();
        Interlocked.Increment(ref _callCount);
        await Task.Delay(200);
        Interlocked.Decrement(ref _callCount);
        return 1;
    }

    public async ValueTask ValueTaskMethod()
    {
        services.IsScoped().Should().BeTrue();
        Interlocked.Increment(ref _callCount);
        await Task.Delay(200);
        Interlocked.Decrement(ref _callCount);
    }

    public async ValueTask<int> ValueTaskIntMethod()
    {
        services.IsScoped().Should().BeTrue();
        Interlocked.Increment(ref _callCount);
        await Task.Delay(200);
        Interlocked.Decrement(ref _callCount);
        return 1;
    }
}

public class ScopedInterceptorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        // Preps
        var services = new ServiceCollection()
            .AddFusion(_ => {}) // Required to make .IsScoped() work
            .AddSingleton(ScopedServiceInterceptor.Options.Default)
            .AddScoped<IScopedServiceTestService, ScopedServiceTestService>()
            .AddRpc(rpc => {
                // That's how you can register a server w/ custom resolver in RPC - in this case
                // it's a proxy for IScopedServiceTestService, which uses ScopedServiceInterceptor.
                rpc.Service(typeof(IScopedServiceTestService)).HasServer(
                    ServiceResolver.New<IScopedServiceTestService>(c => {
                        var interceptorOptions = c.GetRequiredService<ScopedServiceInterceptor.Options>();
                        var interceptor = new ScopedServiceInterceptor(interceptorOptions, c) {
                            ScopedServiceType = typeof(IScopedServiceTestService),
                            MustInterceptSyncCalls = true, // Needed only for this specific test
                        };
                        var proxy = Proxies.New(typeof(IScopedServiceTestService), interceptor);
                        // ReSharper disable once SuspiciousTypeConversion.Global
                        return (IScopedServiceTestService)proxy;
                    }));
            })
            .BuildServiceProvider();

        var serviceDef = services.RpcHub().ServiceRegistry.Get<IScopedServiceTestService>()!;
        var service = (IScopedServiceTestService)serviceDef.Server;

        // Actual test
        service.IntMethod().Should().Be(1);
        service.VoidMethod();
        await service.TaskMethod();
        (await service.TaskIntMethod()).Should().Be(1);
        await service.ValueTaskMethod();
        (await service.ValueTaskIntMethod()).Should().Be(1);
        ScopedServiceTestService.IsDisposeCheckFailed.Value.Should().BeFalse();
    }
}
