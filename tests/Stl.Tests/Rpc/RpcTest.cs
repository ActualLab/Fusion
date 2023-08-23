using Stl.OS;
using Stl.Rpc;
using Stl.Testing.Collections;

namespace Stl.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketTest : RpcTestBase
{
    public RpcWebSocketTest(ITestOutputHelper @out) : base(@out) { }

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
            services.AddSingleton<RpcPeerFactory>(c => static (hub, peerRef) => {
                return peerRef.IsServer
                    ? new RpcServerPeer(hub, peerRef) {
                        LocalServiceFilter = static serviceDef
                            => !serviceDef.IsBackend || serviceDef.Type == typeof(ITestRpcBackend),
                    }
                    : new RpcClientPeer(hub, peerRef);
            });
        }
    }

    [Fact]
    public async Task BasicTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(10, 2)).Should().Be(5);
        (await client.Div(null, 2)).Should().Be(null);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));

        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        await AssertNoCalls(peer);
    }

    [Fact]
    public async Task MulticallTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        var tasks = new Task<int?>[100];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = client.Add(0, i);
        var results = await Task.WhenAll(tasks);
        for (var i = 0; i < results.Length; i++)
            results[i].Should().Be((int?)i);

        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        await AssertNoCalls(peer);
    }

    [Fact]
    public async Task CommandTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var commander = services.Commander();
        await commander.Call(new HelloCommand("ok"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => commander.Call(new HelloCommand("error")));

        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        await AssertNoCalls(peer);
    }

    [Fact]
    public async Task NoWaitTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        // We need to make sure the connection is there before the next call
        await client.Add(1, 1);

        await client.MaybeSet("a", "b");
        await TestExt.WhenMetAsync(async () => {
            var result = await client.Get("a");
            result.Should().Be("b");
        }, TimeSpan.FromSeconds(1));

        await client.MaybeSet("a", "c");
        await TestExt.WhenMetAsync(async () => {
            var result = await client.Get("a");
            result.Should().Be("c");
        }, TimeSpan.FromSeconds(1));

        await AssertNoCalls(peer);
    }

    [Fact]
    public async Task DelayTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        var startedAt = CpuTimestamp.Now;
        await client.Delay(TimeSpan.FromMilliseconds(200));
        startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(100, 500);
        await AssertNoCalls(peer);

        {
            using var cts = new CancellationTokenSource(1);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(0, 500);
            await AssertNoCalls(peer);
        }

        {
            using var cts = new CancellationTokenSource(500);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(300, 1000);
            await AssertNoCalls(peer);
        }
    }

    [Fact]
    public async Task PolymorphTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = ClientServices.GetRequiredService<ITestRpcServiceClient>();
        var backendClient = ClientServices.GetRequiredService<ITestRpcBackendClient>();

        var t = new Tuple<int>(1);
        var t1 = await backendClient.Polymorph(t);
        t1.Should().Be(t);
        t1.Should().NotBeSameAs(t);

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await client.PolymorphArg(new Tuple<int>(1)));
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await client.PolymorphResult(2));

        await AssertNoCalls(peer);
    }

    [Fact]
    public async Task EndpointNotFoundTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        try {
            await client.NoSuchMethod(1, 2, 3, 4);
            Assert.Fail("RpcException wasn't thrown.");
        }
        catch (RpcException e) {
            Out.WriteLine(e.Message);
            e.Message.Should().StartWith("Endpoint not found:");
            e.Message.Should().Contain("NoSuchMethod");
            e.Message.Should().Contain("ITestRpcService");
        }

        await AssertNoCalls(peer);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(50_000)]
    public async Task PerformanceTest(int iterationCount)
    {
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount);
        var tasks = new Task[threadCount];
        await Run(10); // Warmup
        var elapsed = await Run(iterationCount);

        var totalIterationCount = threadCount * iterationCount;
        Out.WriteLine($"{iterationCount}: {totalIterationCount / elapsed.TotalSeconds:F} ops/s using {threadCount} threads");
        await AssertNoCalls(peer);

        async Task<TimeSpan> Run(int count)
        {
            var startedAt = CpuTimestamp.Now;
            for (var threadIndex = 0; threadIndex < threadCount; threadIndex++) {
                tasks[threadIndex] = Task.Run(async () => {
                    for (var i = count; i > 0; i--)
                        if (i != await client.Div(i, 1).ConfigureAwait(false))
                            Assert.Fail("Wrong result.");
                }, CancellationToken.None);
            }

            await Task.WhenAll(tasks);
            return elapsed = startedAt.Elapsed;
        }
    }
}
