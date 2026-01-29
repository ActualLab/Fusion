using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcReconnectionTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        var rpc = services.AddRpc();

        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        commander.AddHandlers<TestRpcService>();
    }

    [Fact]
    public async Task BasicTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await client.Add(1, 1); // Warm-up

        var delay = TimeSpan.FromMilliseconds(100);
        var task = client.Delay(delay);
        await connection.Disconnect();
        await Delay(0.05);
        await connection.Connect();
        (await task).Should().Be(delay);

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task BasicStreamTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var stream = await client.StreamInt32(100, -1, new RandomTimeSpan(0.02, 1));
        var countTask = stream.CountAsync();

        var disruptorCts = new CancellationTokenSource();
        var disruptorTask = ConnectionDisruptor("default", connection, disruptorCts.Token);
        try {
            (await countTask).Should().Be(100);
        }
        finally {
            disruptorCts.CancelAndDisposeSilently();
            await disruptorTask;
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task ConcurrentTest()
    {
        UseLogging = false;
        var workerCount = HardwareInfo.ProcessorCount / 2;
        var testDuration = TimeSpan.FromSeconds(10);
        if (TestRunnerInfo.IsBuildAgent()) {
            workerCount = 1;
            testDuration = TimeSpan.FromSeconds(1);
        }

        var endAt = CpuTimestamp.Now + testDuration;
        var tasks = Enumerable.Range(0, workerCount)
            .Select(i => Task.Run(() => Worker(i.ToString(), endAt)))
            .ToArray();
        await Task.Delay(testDuration);
        var callCount = (await WhenAll(tasks, Out)).Sum();
        WriteLine($"Call count: {callCount}");
        callCount.Should().BeGreaterThan(0);
    }

    private async Task<long> Worker(string workerId, CpuTimestamp endAt)
    {
        // ReSharper disable once LocalFunctionHidesMethod
        void WriteLine(string message)
            => Out.WriteLine($"Worker #{workerId}: {message}");

        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await client.Add(1, 1); // Warm-up

        var rnd = new Random();
        var callCount = 0L;
        var disruptorCts = new CancellationTokenSource();
        var disruptorTask = ConnectionDisruptor(workerId, connection, disruptorCts.Token);
        try {
            while (CpuTimestamp.Now < endAt) {
                try {
                    var maxWaitTime = TimeSpanExt.Min(endAt - CpuTimestamp.Now, TimeSpan.FromSeconds(5));
                    // Ensure maxWaitTime is positive to avoid ArgumentOutOfRangeException
                    if (maxWaitTime <= TimeSpan.Zero)
                        break;

                    var delay = TimeSpan.FromMilliseconds(rnd.Next(5, 120));
                    var delayTask = client.Delay(delay).WaitAsync(maxWaitTime);
                    (await delayTask).Should().Be(delay);
                }
                catch (Exception e) when (e is OperationCanceledException or TimeoutException) {
                    // Intended
                }
                callCount++;
            }
        }
        finally {
            WriteLine("stopping");
            disruptorCts.CancelAndDisposeSilently();
            await disruptorTask;
        }

        await AssertNoCalls(connection.ClientPeer, Out);
        await AssertNoCalls(connection.ServerPeer, Out);
        return callCount;
    }

    [Fact(Timeout = 40_000)]
    public async Task ConcurrentStreamTest()
    {
        UseLogging = false;
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var workerCount = 2;
        if (TestRunnerInfo.IsBuildAgent())
            workerCount = 1;
        var tasks = Enumerable.Range(0, workerCount)
            .Select(async workerId => {
                var totalCount = 300;
                var stream = await client.StreamInt32(totalCount, -1, new RandomTimeSpan(0.02, 1));
                var count = 0;
                await foreach (var item in stream) {
                    count++;
                    if (item % 10 == 0)
                        WriteLine($"{workerId}: {item}");
                }
                count.Should().Be(totalCount);
            })
            .ToArray();

        var disruptorCts = new CancellationTokenSource();
        var disruptorTask = ConnectionDisruptor("default", connection, disruptorCts.Token);
        try {
            await Task.WhenAll(tasks);
        }
        finally {
            disruptorCts.CancelAndDisposeSilently();
            await disruptorTask;
        }
    }

    // Protected methods

    protected override ServiceProvider CreateServices(Action<IServiceCollection>? configureServices = null)
        => base.CreateServices(services => {
            services.AddRpc().AddMiddleware<RpcInboundCallDelayer>();
            configureServices?.Invoke(services);
        });
}
