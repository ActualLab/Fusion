using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcLoopbackConnectionTest(ITestOutputHelper @out)
    : RpcLocalConnectionTestBase(RpcPeerConnectionKind.Loopback, @out);

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcLocalCallConnectionTest(ITestOutputHelper @out)
    : RpcLocalConnectionTestBase(RpcPeerConnectionKind.LocalCall, @out);

public abstract class RpcLocalConnectionTestBase : RpcTestBase
{
    protected RpcLocalConnectionTestBase(RpcPeerConnectionKind connectionKind, ITestOutputHelper @out) : base(@out)
    {
        ConnectionKind = connectionKind;
        ExposeBackend = true;
    }

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient)
            throw new InvalidOperationException("Client shouldn't be used in this test.");

        rpc.AddServer<ITestRpcService, TestRpcService>();
        rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
        if (ConnectionKind == RpcPeerConnectionKind.LocalCall) {
            rpc.AddClient<ITestRpcService, TestRpcService>();
            rpc.AddClient<ITestRpcBackend, TestRpcBackend>();
        }
        else {
            rpc.AddClient<ITestRpcService>();
            rpc.AddClient<ITestRpcBackend>();
        }
        commander.AddHandlers<TestRpcService>();
        commander.AddHandlers<TestRpcBackend>();
    }

    [Fact]
    public async Task BasicTest()
    {
        await using var _ = await WebHost.Serve();
        var client = WebHost.Services.GetRequiredService<ITestRpcService>();
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(10, 2)).Should().Be(5);
        (await client.Div(null, 2)).Should().Be(null);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));
    }

    [Fact]
    public async Task PolymorphTest()
    {
        await using var _ = await WebHost.Serve();
        var client = WebHost.Services.GetRequiredService<ITestRpcService>();
        var backendClient = WebHost.Services.GetRequiredService<ITestRpcBackend>();

        var t = new Tuple<int>(1);
        var t1 = await backendClient.Polymorph(t);
        t1.Should().Be(t);

        await client.PolymorphArg(new Tuple<int>(1)); // No checks w/ local connection
        await client.PolymorphResult(2); // No checks w/ local connection
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(50_000)]
    public async Task PerformanceTest(int iterationCount)
    {
        // ByteSerializer.Default = MessagePackByteSerializer.Default;
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        await using var _ = await WebHost.Serve();
        var client = WebHost.Services.GetRequiredService<ITestRpcService>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount / 4);
        var tasks = new Task[threadCount];
        await Run(10); // Warmup
        var elapsed = await Run(iterationCount);

        var totalIterationCount = threadCount * iterationCount;
        Out.WriteLine($"{iterationCount}: {totalIterationCount / elapsed.TotalSeconds:F} ops/s using {threadCount} threads");

        async Task<TimeSpan> Run(int count)
        {
            var startedAt = CpuTimestamp.Now;
            for (var threadIndex = 0; threadIndex < threadCount; threadIndex++) {
                tasks[threadIndex] = Task.Run(() =>
                    Enumerable
                        .Range(0, count)
                        .Select(async i => {
                            if (i != await client.Div(i, 1).ConfigureAwait(false))
                                Assert.Fail("Wrong result.");
                        })
                        .Collect(256),
                    CancellationToken.None);
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

        await using var _ = await WebHost.Serve();
        var client = WebHost.Services.GetRequiredService<ITestRpcService>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount / 2);
        var tasks = new Task[threadCount];
        await Run(10); // Warmup
        var elapsed = await Run(itemCount);

        var totalItemCount = threadCount * itemCount;
        Out.WriteLine($"{itemCount}: {totalItemCount / elapsed.TotalSeconds:F} ops/s using {threadCount} threads");

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
}
