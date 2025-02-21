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

public class ScopedServiceTestService : IScopedServiceTestService
{
    public void VoidMethod()
    { }

    public int IntMethod()
        => 1;

    public Task TaskMethod()
        => Task.CompletedTask;

    public Task<int> TaskIntMethod()
        => Task.FromResult(1);

    public ValueTask ValueTaskMethod()
        => default;

    public ValueTask<int> ValueTaskIntMethod()
        => new(1);
}

public class ScopedInterceptorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        // Preps
        var services = new ServiceCollection()
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
