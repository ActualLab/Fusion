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
        SerializationFormat = "msgpack4c";
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
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    [InlineData(1, 3, 10_000)]
    public async Task GetBytesTest(int passCount, int taskCount, int itemCount)
    {
        WriteLine($"Parameters: {passCount}p x {taskCount}t x {itemCount}");
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcService>();

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
            WriteLine($"Pass time: {startedAt.Elapsed.ToShortString()}");
        }
    }

    [Theory]
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    [InlineData(1, 3, 10_000)]
    public async Task GetMemoryTest(int passCount, int taskCount, int itemCount)
    {
        WriteLine($"Parameters: {passCount}p x {taskCount}t x {itemCount}");
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcService>();

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
            WriteLine($"Pass time: {startedAt.Elapsed.ToShortString()}");
        }
    }
}
