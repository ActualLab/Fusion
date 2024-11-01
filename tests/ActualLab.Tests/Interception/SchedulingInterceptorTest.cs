using ActualLab.Concurrency;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Tests.Interception;

public class SchedulingTestService : IRequiresAsyncProxy, IHasTaskFactory
{
    private TaskFactory? _taskFactory;
    private ConcurrentExclusiveSchedulerPair Schedulers { get; } = new();

    TaskFactory IHasTaskFactory.TaskFactory => _taskFactory ??= new(Schedulers.ConcurrentScheduler);

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
        return Task.FromResult(0);
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
        return default;
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
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton(_ => SchedulingInterceptor.Options.Default)
        .BuildServiceProvider();

    [Fact]
    public async Task BasicTest()
    {
        var interceptorOptions = Services.GetRequiredService<SchedulingInterceptor.Options>();
        var interceptor = new SchedulingInterceptor(interceptorOptions, Services) {
            TaskFactoryResolver = static invocation => invocation.Method.Name.StartsWith("NonScheduled")
                ? null
                : (invocation.Proxy as IHasTaskFactory)?.TaskFactory,
        };
        // ReSharper disable once SuspiciousTypeConversion.Global
        var proxy = (SchedulingTestService)Proxies.New(typeof(SchedulingTestService), interceptor);
        await proxy.TaskMethod();
        await proxy.TaskIntMethod();
        await proxy.ValueTaskMethod();
        await proxy.ValueTaskIntMethod();
        await proxy.NonScheduledTaskMethod();
    }
}
