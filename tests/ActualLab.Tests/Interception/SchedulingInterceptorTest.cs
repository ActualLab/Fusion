using ActualLab.Concurrency;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Tests.Interception;

public class SchedulingTestService : IRequiresAsyncProxy, IHasTaskFactory
{
    private ConcurrentExclusiveSchedulerPair Schedulers { get; } = new();

    TaskFactory IHasTaskFactory.TaskFactory => field ??= new(Schedulers.ConcurrentScheduler);

    public virtual Task TaskMethod()
    {
        var scheduler = TaskScheduler.Current;
        scheduler.Should().Be(Schedulers.ConcurrentScheduler);
        return Task.CompletedTask;
    }

    public virtual Task<int> TaskIntMethod()
    {
        var scheduler = TaskScheduler.Current;
        scheduler.Should().Be(Schedulers.ConcurrentScheduler);
        return Task.FromResult(1);
    }

    public virtual ValueTask ValueTaskMethod()
    {
        var scheduler = TaskScheduler.Current;
        scheduler.Should().Be(Schedulers.ConcurrentScheduler);
        return default;
    }

    public virtual ValueTask<int> ValueTaskIntMethod()
    {
        var scheduler = TaskScheduler.Current;
        scheduler.Should().Be(Schedulers.ConcurrentScheduler);
        return new(1);
    }

    public virtual Task NonScheduledTaskMethod()
    {
        var scheduler = TaskScheduler.Current;
        scheduler.Should().NotBe(Schedulers.ConcurrentScheduler);
        return Task.CompletedTask;
    }
}

public class SchedulingInterceptorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        // Preps
        var services = new ServiceCollection().BuildServiceProvider();
        var interceptorOptions = SchedulingInterceptor.Options.Default;
        var interceptor = new SchedulingInterceptor(interceptorOptions, services) {
            TaskFactoryResolver = static invocation => invocation.Method.Name.StartsWith("NonScheduled")
                ? null
                : (invocation.Proxy as IHasTaskFactory)?.TaskFactory,
        };
        // ReSharper disable once SuspiciousTypeConversion.Global
        var proxy = (SchedulingTestService)Proxies.New(typeof(SchedulingTestService), interceptor);

        // Actual test
        await proxy.TaskMethod();
        (await proxy.TaskIntMethod()).Should().Be(1);
        await proxy.ValueTaskMethod();
        (await proxy.ValueTaskIntMethod()).Should().Be(1);
        await proxy.NonScheduledTaskMethod();
    }
}
