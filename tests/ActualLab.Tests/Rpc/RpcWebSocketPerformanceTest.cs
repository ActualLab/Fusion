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
        UseFastRpcByteSerializer = true;
    }

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcServiceClient>();
            rpc.Service<ITestRpcServiceClient>().HasName(nameof(ITestRpcService));
            commander.AddService<ITestRpcServiceClient>();
            rpc.AddClient<ITestRpcBackend, ITestRpcBackendClient>();
            commander.AddService<ITestRpcBackendClient>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
            rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
            commander.AddService<TestRpcBackend>();
        }
    }

    [Theory]
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    public async Task GetBytesTest(int passCount, int threadCount, int itemCount)
    {
        Out.WriteLine($"Thread count: {threadCount}");
        Out.WriteLine($"Item count: {itemCount}");
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        for (var p = 0; p < passCount; p++) {
            var startedAt = CpuTimestamp.Now;
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(async () => {
                for (var i = 0; i < itemCount; i++) {
                    var size = Math.Min(20_000, i * 100);
                    var data = await client.GetBytes(size).ConfigureAwait(false);
                    data.Length.Should().Be(size);
                }
            }));
            await Task.WhenAll(tasks);
            Out.WriteLine($"Pass time: {startedAt.Elapsed.ToShortString()}");
        }
    }

    [Theory]
    [InlineData(1, 1, 300)]
    [InlineData(1, 3, 100)]
    [InlineData(1, 3, 1_000)]
    public async Task GetMemoryTest(int passCount, int threadCount, int itemCount)
    {
        Out.WriteLine($"Thread count: {threadCount}");
        Out.WriteLine($"Item count: {itemCount}");
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        for (var p = 0; p < passCount; p++) {
            var startedAt = CpuTimestamp.Now;
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(async () => {
                for (var i = 0; i < itemCount; i++) {
                    var size = Math.Min(20_000, i * 100);
                    var data = await client.GetMemory(size).ConfigureAwait(false);
                    data.Length.Should().Be(size);
                }
            }));
            await Task.WhenAll(tasks);
            Out.WriteLine($"Pass time: {startedAt.Elapsed.ToShortString()}");
        }
    }
}
