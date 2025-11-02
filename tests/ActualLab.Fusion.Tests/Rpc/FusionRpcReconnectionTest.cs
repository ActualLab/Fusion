using ActualLab.Fusion.Tests.Services;
using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcReconnectionTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IReconnectTester, ReconnectTester>();
    }

    [Fact]
    public async Task Case1Test()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IReconnectTester>();

        var (delay, invDelay) = (300, 300);
        var task = client.Delay(delay, invDelay);

        await Delay(0.05);
        await connection.Reconnect();
        // Call is still running on server, so recovery will pull its result

        (await task.WaitAsync(TimeSpan.FromSeconds(1))).Should().Be((delay, invDelay));
        var computed = await Computed
            .Capture(() => client.Delay(delay, invDelay))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(0.1)); // Should be instant
        computed.IsConsistent().Should().BeTrue();

        var startedAt = CpuTimestamp.Now;
        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(1));
        var elapsed = CpuTimestamp.Now - startedAt;
        if (!TestRunnerInfo.IsBuildAgent())
            elapsed.TotalSeconds.Should().BeGreaterThan(0.1);

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task Case2Test()
    {
        var waitMultiplier = TestRunnerInfo.IsBuildAgent() ? 10 : 1;
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IReconnectTester>();

        var (delay, invDelay) = (300, 300);
        var task = client.Delay(delay, invDelay);
        (await task.WaitAsync(TimeSpan.FromSeconds(1 * waitMultiplier))).Should().Be((delay, invDelay));
        var computed = await Computed
            .Capture(() => client.Delay(delay, invDelay))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(0.1)); // Should be instant
        computed.IsConsistent().Should().BeTrue();
        computed.Invalidated += _ => {
            Out.WriteLine("Invalidated: {0}", new StackTrace());
        };

        await connection.Reconnect();
        // Recovery is expected to trigger result update and/or invalidation

        var startedAt = CpuTimestamp.Now;
        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(1 * waitMultiplier));
        var elapsed = CpuTimestamp.Now - startedAt;
        if (!TestRunnerInfo.IsBuildAgent())
            elapsed.TotalSeconds.Should().BeGreaterThan(0.1);

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task Case3Test()
    {
        var waitMultiplier = TestRunnerInfo.IsBuildAgent() ? 10 : 1;
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IReconnectTester>();

        var (delay, invDelay) = (200, 300);
        var task = client.Delay(delay, invDelay);

        await Task.Delay(100);
        // The call is sent by now, so recovery is expected to reconnect the call
        await connection.Reconnect(TimeSpan.FromMilliseconds(1));

        // Recovery is expected to reconnect the call
        var result = await task.WaitAsync(TimeSpan.FromSeconds(1 * waitMultiplier));
        result.Should().Be((delay, invDelay));

        // We've just got the result, so repeated call should resolve from cache
        var computed = await Computed
            .Capture(() => client.Delay(delay, invDelay))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(0.1));

        var startedAt = CpuTimestamp.Now;
        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(1 * waitMultiplier));
        var elapsed = CpuTimestamp.Now - startedAt;
        if (!TestRunnerInfo.IsBuildAgent())
            elapsed.TotalSeconds.Should().BeGreaterThan(0.1);

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task Case4Test()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IReconnectTester>();

        var c1 = await Computed.Capture(() => client.GetTime());
        c1.Invalidate();
        var c1a = await Computed.Capture(() => client.GetTime());
        c1a.Value.Should().Be(c1.Value);
        c1 = c1a;

        await connection.Reconnect();
        var c2 = await Computed.Capture(() => client.GetTime());
        c2.Should().BeSameAs(c1);
        c2.Invalidate();
        var c2a = await c2.UpdateUntyped();
        c2a.Value.Should().Be(c1.Value);
    }

    [Fact]
    public async Task Case5Test()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IReconnectTester>();
        var server = services.GetRequiredService<ReconnectTester>();

        for (var i = 0; i < 50; i++) {
            var ctTask = client.GetTime();
            server.InvalidateGetTime();
            var stTask = server.GetTime();
            await connection.Reconnect();
            var st = await stTask;
            var ct = await ctTask;
            if (ct == st)
                Out.WriteLine($"{i}: In sync");
            else {
                Out.WriteLine($"{i}: Syncing...");
                var c = await Computed.Capture(() => client.GetTime());
                await c.When(x => x >= st).WaitAsync(TimeSpan.FromSeconds(3));
                Out.WriteLine($"{i}: Synced: {c}");
            }
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task ReconnectionTest()
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
        var counts = (await WhenAll(tasks, Out)).Aggregate(new long[3], (x, y) => [x[0] + y[0], x[1] + y[1], x[2] + y[2]]);
        Out.WriteLine($"Call counts: {counts[0]} ok, {counts[1]} timeouts, {counts[2]} cancellations");
        counts[0].Should().BeGreaterThan(0);
    }

    private async Task<long[]> Worker(string workerId, CpuTimestamp endAt)
    {
        void Write(string message)
            => Out.WriteLine($"Worker #{workerId}: {message}");

        Write("started");
        var (successCount, timeoutCount, cancellationCount) = (0L, 0L, 0L);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Connections.First().Value;
        var client = services.RpcHub().GetClient<IReconnectTester>();
        await client.Delay(1, 1); // Warm-up

        var disruptorCts = new CancellationTokenSource();
        var disruptorTask = ConnectionDisruptor(workerId, connection, disruptorCts.Token);
        try {
            var rnd = new Random();
            while (CpuTimestamp.Now < endAt) {
                var delay = rnd.Next(10, 100);
                var invDelay = rnd.Next(10, 100);
                var maxWaitTime = TimeSpanExt.Min(endAt - CpuTimestamp.Now, TimeSpan.FromSeconds(10));
                if (rnd.NextDouble() < 0.5) {
                    // Timeout case
                    try {
                        var result = await client.Delay(delay, invDelay).WaitAsync(maxWaitTime);
                        result.Should().Be((delay, invDelay));
                        successCount++;
                    }
                    catch (TimeoutException) {
                        timeoutCount++;
                    }
                }
                else {
                    // Cancellation case
                    var timeoutCts = new CancellationTokenSource(maxWaitTime);
                    try {
                        var result = await client.Delay(delay, invDelay, timeoutCts.Token);
                        result.Should().Be((delay, invDelay));
                        successCount++;
                    }
                    catch (OperationCanceledException) {
                        cancellationCount++;
                    }
                    finally {
                        timeoutCts.CancelAndDisposeSilently();
                    }
                }
            }
        }
        finally {
            Write("stopping ConnectionDisruptor");
            disruptorCts.CancelAndDisposeSilently();
            await disruptorTask;
        }

        Write($"{successCount} ok, {timeoutCount} timeouts, {cancellationCount} cancellations");
        // await AssertNoCalls(connection.ClientPeer, Out);
        // await AssertNoCalls(connection.ServerPeer, Out);
        return [successCount, timeoutCount, cancellationCount];
    }

    // Protected methods

    protected override ServiceProvider CreateServices(Action<IServiceCollection>? configureServices = null)
        => base.CreateServices(services => {
            services.AddRpc().AddInboundMiddleware(c => new RpcRandomDelayMiddleware(c));
            configureServices?.Invoke(services);
        });
}
