using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketPerformanceTest : RpcTestBase
{
    public RpcWebSocketPerformanceTest(ITestOutputHelper @out) : base(@out)
    {
        ExposeBackend = true;
        RpcFrameDelayerFactory = null;
        SerializationFormat = "msgpack6c";
    }

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcService>();
            commander.AddService<ITestRpcService>();
            rpc.AddClient<ITestRpcBackend>();
            commander.AddService<ITestRpcBackend>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
            rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
            commander.AddService<TestRpcBackend>();
        }
    }

    [Theory]
    [InlineData(1, 10_000)]
    // [InlineData(32, 200_000)]
    // [InlineData(64, 100_000)]
    // [InlineData(128, 50_000)]
    // [InlineData(256, 25_000)]
    // [InlineData(512, 12_500)]
    // [InlineData(1024, 6_250)]
    public async Task AddTest(int taskCount, int itemCount)
    {
        WriteLine($"Parameters: {taskCount}t x {itemCount}");
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        for (var p = 0; p < 2; p++) {
            var passItemCount = p >= 1 ? itemCount : itemCount / 10;
            var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () => {
                for (var i = passItemCount; i > 0; i--) {
                    var result = await client.Add(i, i).ConfigureAwait(false);
                    result.Should().Be(i << 1);
                }
            }));
            var startedAt = CpuTimestamp.Now;
            await Task.WhenAll(tasks);
            if (p >= 1)
                WriteLine($"Pass time: {startedAt.Elapsed.ToShortString()}");
        }
    }

    [Theory]
    // Fastest options (compact)
    [InlineData(200_000, "mempack6c")]
    [InlineData(200_000, "msgpack6c")]
    [InlineData(200_000, "mempack5c")]
    [InlineData(200_000, "msgpack5c")]
    // v5 formats
    [InlineData(30_000, "json5")]
    [InlineData(30_000, "njson5")]
    [InlineData(30_000, "mempack5")]
    [InlineData(30_000, "msgpack5")]
    public async Task PerformanceTest(int iterationCount, string? serializationFormat = null)
    {
        SerializationFormat = serializationFormat ?? SerializationFormat;
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount / 2);
        var tasks = new Task[threadCount];
        await client.Div(1, 1).ConfigureAwait(false);
        await Run(100); // Warmup
        GC.Collect();

        WriteLine($"{iterationCount} iterations x {threadCount} threads:");
        var elapsed = await Run(iterationCount);
        var totalIterationCount = threadCount * iterationCount;
        WriteLine($"{totalIterationCount / elapsed.TotalSeconds:F} ops/s using {threadCount} threads");

        await AssertNoCalls(peer, Out);
        return;

        async Task<TimeSpan> Run(int count) {
            var startedAt = CpuTimestamp.Now;
            for (var threadIndex = 0; threadIndex < threadCount; threadIndex++) {
                tasks[threadIndex] = Task.Run(async () => {
                    for (var i = 0; i < count; i++) {
                        if (i != await client.Div(i, 1).ConfigureAwait(false))
                            Assert.Fail("Wrong result.");
                    }
                }, CancellationToken.None);
            }

            await Task.WhenAll(tasks);
            return elapsed = startedAt.Elapsed;
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10_000)]
    public async Task StreamPerformanceTest(int itemCount)
    {
        if (TestRunnerInfo.IsBuildAgent())
            itemCount = 100;

        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount / 2);
        var tasks = new Task[threadCount];
        await Run(10); // Warmup
        var elapsed = await Run(itemCount);

        var totalItemCount = threadCount * itemCount;
        WriteLine($"{itemCount}: {totalItemCount / elapsed.TotalSeconds:F} ops/s using {threadCount} threads");
        await AssertNoCalls(peer, Out);
        return;

        async Task<TimeSpan> Run(int count)
        {
            var startedAt = CpuTimestamp.Now;
            for (var threadIndex = 0; threadIndex < threadCount; threadIndex++) {
                tasks[threadIndex] = Task.Run(async () => {
                    var stream = await client.StreamInt32(count);
                    (await stream.CountAsync()).Should().Be(count);
                }, CancellationToken.None);
            }

            await Task.WhenAll(tasks);
            return elapsed = startedAt.Elapsed;
        }
    }

    [Theory]
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    // [InlineData(1, 3, 10_000)]
    public async Task GetBytesTest(int passCount, int taskCount, int itemCount)
    {
        WriteLine($"Parameters: {passCount}p x {taskCount}t x {itemCount}");
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // Calculate total bytes per task
        var totalBytesPerTask = 0L;
        for (var i = 0; i < itemCount; i++)
            totalBytesPerTask += Math.Min(20_000, i * 100);
        var totalBytes = totalBytesPerTask * taskCount;

        for (var p = 0; p < passCount; p++) {
            var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () => {
                for (var i = 0; i < itemCount; i++) {
                    var size = Math.Min(20_000, i * 100);
                    var data = await client.GetBytes(size).ConfigureAwait(false);
                    data.Length.Should().Be(size);
                }
            }));
            var startedAt = CpuTimestamp.Now;
            await Task.WhenAll(tasks);
            var elapsed = startedAt.Elapsed;
            var mbPerSecond = totalBytes / elapsed.TotalSeconds / (1024 * 1024);
            WriteLine($"Pass time: {elapsed.ToShortString()}, {mbPerSecond:F2} MB/s");
        }
    }

    [Theory]
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    // [InlineData(1, 3, 10_000)]
    public async Task GetMemoryTest(int passCount, int taskCount, int itemCount)
    {
        WriteLine($"Parameters: {passCount}p x {taskCount}t x {itemCount}");
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // Calculate total bytes per task
        var totalBytesPerTask = 0L;
        for (var i = 0; i < itemCount; i++)
            totalBytesPerTask += Math.Min(20_000, i * 100);
        var totalBytes = totalBytesPerTask * taskCount;

        for (var p = 0; p < passCount; p++) {
            var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () => {
                for (var i = 0; i < itemCount; i++) {
                    var size = Math.Min(20_000, i * 100);
                    var data = await client.GetMemory(size).ConfigureAwait(false);
                    data.Length.Should().Be(size);
                }
            }));
            var startedAt = CpuTimestamp.Now;
            await Task.WhenAll(tasks);
            var elapsed = startedAt.Elapsed;
            var mbPerSecond = totalBytes / elapsed.TotalSeconds / (1024 * 1024);
            WriteLine($"Pass time: {elapsed.ToShortString()}, {mbPerSecond:F2} MB/s");
        }
    }
}
