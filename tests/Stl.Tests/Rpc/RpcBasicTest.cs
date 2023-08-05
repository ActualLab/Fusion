using Stl.Rpc;
using Stl.Rpc.Infrastructure;
using Stl.Rpc.Testing;
using Stl.Testing.Collections;

namespace Stl.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcBasicTest : RpcLocalTestBase
{
    public RpcBasicTest(ITestOutputHelper @out) : base(@out) { }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();
        commander.AddService<TestRpcBackend>();

        var rpc = services.AddRpc();
        rpc.AddServer<ITestRpcService, TestRpcService>();
        rpc.AddClient<ITestRpcService, ITestRpcServiceClient>();
        rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
        rpc.AddClient<ITestRpcBackend, ITestRpcBackendClient>();
        services.AddSingleton<RpcPeerFactory>(_ => static (hub, peerRef) => {
            return peerRef.IsServer
                ? new RpcServerPeer(hub, peerRef) {
                    LocalServiceFilter = static serviceDef
                        => !serviceDef.IsBackend || serviceDef.Type == typeof(ITestRpcBackend),
                }
                : new RpcClientPeer(hub, peerRef);
        });
    }

    [Fact]
    public async Task BasicTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(10, 2)).Should().Be(5);
        (await client.Div(null, 2)).Should().Be(null);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));
        await AssertNoCalls(clientPeer);
    }

    [Fact]
    public async Task CommandTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().Single();
        var clientPeer = connection.ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        (await client.OnHello(new HelloCommand("X"))).Should().Be("Hello, X!");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.OnHello(new HelloCommand("error")));

        connection.Disconnect();
        await clientPeer.ConnectionState.WhenDisconnected();
        await Assert.ThrowsAsync<DisconnectedException>(
            () => client.OnHello(new HelloCommand("X", TimeSpan.FromSeconds(2))));
        await Delay(0.1);
        await AssertNoCalls(clientPeer);

        await connection.Connect();
        await Assert.ThrowsAsync<TimeoutException>(
            () => client.OnHello(new HelloCommand("X", TimeSpan.FromSeconds(11))));

        await AssertNoCalls(clientPeer);
    }

    [Fact]
    public async Task NoWaitTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
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

        await AssertNoCalls(clientPeer);
    }

    [Fact]
    public async Task DelayTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        await client.Add(1, 1); // Warm-up

        var startedAt = CpuTimestamp.Now;
        await client.Delay(TimeSpan.FromMilliseconds(200));
        startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(100, 500);
        await AssertNoCalls(clientPeer);

        {
            using var cts = new CancellationTokenSource(1);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(0, 500);
            await AssertNoCalls(clientPeer);
        }

        {
            using var cts = new CancellationTokenSource(500);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(300, 1000);
            await AssertNoCalls(clientPeer);
        }
    }

    [Fact]
    public async Task PolymorphTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        var backendClient = services.GetRequiredService<ITestRpcBackendClient>();

        var t = new Tuple<int>(1);
        (await backendClient.Polymorph(t)).Should().Be(t);

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await client.PolymorphArg(new Tuple<int>(1)));
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await client.PolymorphResult(2));

        await AssertNoCalls(clientPeer);
    }

    [Fact]
    public async Task CancellationTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();

        var cts = new CancellationTokenSource(100);
        var result = await client.Delay(TimeSpan.FromMilliseconds(300), cts.Token).ResultAwait();
        result.Error.Should().BeAssignableTo<OperationCanceledException>();
        var cancellationCount = await client.GetCancellationCount();
        cancellationCount.Should().Be(1);
        await AssertNoCalls(clientPeer);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10_000)]
    [InlineData(50_000)]
    [InlineData(200_000)]
    public async Task PerformanceTest(int iterationCount)
    {
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().Single().ClientPeer;
        var client = services.GetRequiredService<ITestRpcServiceClient>();
        await client.Div(1, 1);
        await AssertNoCalls(clientPeer);

        var startedAt = CpuTimestamp.Now;
        for (var i = iterationCount; i > 0; i--)
            if (i != await client.Add(i, 0).ConfigureAwait(false))
                Assert.Fail("Wrong result.");
        var elapsed = startedAt.Elapsed;
        Out.WriteLine($"{iterationCount}: {iterationCount / elapsed.TotalSeconds:F} ops/s");
        await AssertNoCalls(clientPeer);
    }
}
