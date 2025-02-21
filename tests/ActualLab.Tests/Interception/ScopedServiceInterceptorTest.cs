using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Tests.Interception;

public interface IScopedServiceTestService : IRequiresFullProxy
{
    void VoidMethod();
    int IntMethod();
    Task TaskMethod();
    Task<int> TaskIntMethod();
    ValueTask ValueTaskMethod();
    ValueTask<int> ValueTaskIntMethod();
}

public class ScopedServiceTestService(IServiceProvider services) : IScopedServiceTestService
{
    public void VoidMethod()
    {
        services.IsScoped().Should().BeTrue();
    }

    public int IntMethod()
    {
        services.IsScoped().Should().BeTrue();
        return 1;
    }

    public Task TaskMethod()
    {
        services.IsScoped().Should().BeTrue();
        return Task.CompletedTask;
    }

    public Task<int> TaskIntMethod()
    {
        services.IsScoped().Should().BeTrue();
        return Task.FromResult(1);
    }

    public ValueTask ValueTaskMethod()
    {
        services.IsScoped().Should().BeTrue();
        return default;
    }

    public ValueTask<int> ValueTaskIntMethod()
    {
        services.IsScoped().Should().BeTrue();
        return new ValueTask<int>(1);
    }
}

public class ScopedInterceptorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        // Preps
        var services = new ServiceCollection()
            .AddCommander().Services // Required to make .IsScoped() work
            .AddScoped<IScopedServiceTestService, ScopedServiceTestService>()
            .BuildServiceProvider();
        var interceptorOptions = ScopedServiceInterceptor.Options.Default;
        var interceptor = new ScopedServiceInterceptor(interceptorOptions, services) {
            ScopedServiceType = typeof(IScopedServiceTestService),
            MustInterceptSyncCalls = true,
        };
        // ReSharper disable once SuspiciousTypeConversion.Global
        var proxy = (IScopedServiceTestService)Proxies.New(typeof(IScopedServiceTestService), interceptor);

        // Actual test
        proxy.IntMethod().Should().Be(1);
        proxy.VoidMethod();
        await proxy.TaskMethod();
        (await proxy.TaskIntMethod()).Should().Be(1);
        await proxy.ValueTaskMethod();
        (await proxy.ValueTaskIntMethod()).Should().Be(1);
    }
}
