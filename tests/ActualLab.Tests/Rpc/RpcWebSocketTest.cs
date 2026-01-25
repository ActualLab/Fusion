using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketTest : RpcTestBase
{
    public RpcWebSocketTest(ITestOutputHelper @out) : base(@out)
        => ExposeBackend = true;

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcServiceClient>(nameof(ITestRpcService));
            commander.AddService<ITestRpcServiceClient>();
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
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    public async Task BasicTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(10, 2)).Should().Be(5);
        (await client.Div(null, 2)).Should().Be(null);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));

        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        await AssertNoCalls(peer, Out);
    }

    [Fact]
    public async Task MulticallTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var tasks = new Task<int?>[100];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = client.Add(0, i);
        var results = await Task.WhenAll(tasks);
        for (var i = 0; i < results.Length; i++)
            results[i].Should().Be((int?)i);

        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        await AssertNoCalls(peer, Out);
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
        await AssertNoCalls(peer, Out);
    }

    [Fact]
    public async Task NoWaitTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        // We need to make sure the connection is there before the next call
        await client.Add(1, 1);

        await client.MaybeSet("a", "b");
        await TestExt.When(async () => {
            var result = await client.Get("a");
            result.Should().Be("b");
        }, TimeSpan.FromSeconds(1));

        await client.MaybeSet("a", "c");
        await TestExt.When(async () => {
            var result = await client.Get("a");
            result.Should().Be("c");
        }, TimeSpan.FromSeconds(1));

        await AssertNoCalls(peer, Out);
    }

    [Fact]
    public async Task DelayTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var startedAt = CpuTimestamp.Now;
        await client.Delay(TimeSpan.FromMilliseconds(200));
        startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(100, 500);
        await AssertNoCalls(peer, Out);

        {
            using var cts = new CancellationTokenSource(1);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(0, 500);
            await AssertNoCalls(peer, Out);
        }

        {
            using var cts = new CancellationTokenSource(500);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(300, 1000);
            await AssertNoCalls(peer, Out);
        }
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    public async Task PolymorphTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();
        var backendClient = services.RpcHub().GetClient<ITestRpcBackend>();
        var clientPeer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var backendClientPeer = services.RpcHub().GetClientPeer(BackendClientPeerRef);

        var t = new Tuple<int>(1);
        var t1 = await backendClient.Polymorph(t);
        t1.Should().Be(t);
        t1.Should().NotBeSameAs(t);

        (await client.PolymorphArg(new Tuple<int>(1))).Should().Be(1);
        (await client.PolymorphResult(2)).Should().Be(new Tuple<int>(2));

        await AssertNoCalls(clientPeer, Out);
        await AssertNoCalls(backendClientPeer, Out);
    }

    [Fact]
    public async Task EndpointNotFoundTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        try {
            await client.NoSuchMethod(1, 2, 3, 4);
            Assert.Fail("RpcException wasn't thrown.");
        }
        catch (RpcException e) {
            WriteLine(e.Message);
            e.Message.Should().StartWith("Endpoint not found:");
            e.Message.Should().ContainAny("NoSuchMethod", "Service.Method<");
        }

        await AssertNoCalls(peer, Out);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    public async Task StreamTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var expected1 = Enumerable.Range(0, 500).ToList();
        var stream1 = await client.StreamInt32(expected1.Count);
        (await stream1.ToListAsync()).Should().Equal(expected1);
        await AssertNoCalls(peer, Out);
        if (SerializationFormat.EndsWith("-np"))
            return;

        var expected2 = Enumerable.Range(0, 500)
            .Select(x => (x & 2) == 0 ? (ITuple)new Tuple<int>(x) : new Tuple<long>(x))
            .ToList();
        var stream2 = await client.StreamTuples(expected2.Count);
        (await stream2.ToListAsync()).Should().Equal(expected2);
        await AssertNoCalls(peer, Out);

        var stream3 = await client.StreamTuples(10, 5);
        (await stream3.Take(5).CountAsync()).Should().Be(5);

        var stream3f = await client.StreamTuples(10, 5);
        try {
            await stream3f.CountAsync();
            Assert.Fail("No exception!");
        }
        catch (Exception e) {
            e.Should().BeOfType<InvalidOperationException>();
        }
        await AssertNoCalls(peer, Out);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    public async Task StreamInputTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        using var cts = new CancellationTokenSource();
        for (var length = 0; length < 100; length++) {
            var seq = Enumerable.Range(0, length);
            var count = await client.Count(RpcStream.New(seq), cts.Token);
            count.Should().Be(length);
        }
    }

    [Fact]
    public async Task StreamLagTest()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();
        var systemClock = services.Clocks().SystemClock;

        (await client.Div(6, 2)).Should().Be(3); // To make sure everything is set up
        var tasks = new List<Task>();
        var countRandom = new Random(31);
        for (var iteration = 0; iteration < 200; iteration++) {
            var count = countRandom.Next(30);
            var stream = RpcStream.New(GetStream(iteration, count));
            tasks.Add(client.CheckLag(stream, count));
        }
        await Task.WhenAll(tasks);
        return;

        async IAsyncEnumerable<Moment> GetStream(int iteration, int count)
        {
            var random = new Random(33 + iteration);
            for (var i = 0; i < count; i++) {
                if (random.NextDouble() < 0.2)
                    await Task.Delay(200);
                yield return systemClock.Now;
            }
        }
    }

    [Fact]
    public async Task StreamDebugTest()
    {
        RpcFrameDelayerFactory = default;
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var tasks = new Task<List<int>>[3];
        for (var i = 0; i < tasks.Length; i++) {
            var taskIndex = i;
            tasks[i] = Task.Run(async () => {
                var stream = await client.StreamInt32(100_000);
                var list = new List<int>();
                await foreach (var j in stream.ConfigureAwait(false)) {
                    if (j % 1000 == 0)
                        WriteLine($"{taskIndex}: {j}");
                    list.Add(j);
                }
                return list;
            }, CancellationToken.None);
        }
        await Task.WhenAll(tasks);
        await AssertNoCalls(peer, Out);
    }

    [Theory]
    [InlineData(50_000, "json5")]
    [InlineData(50_000, "njson5")]
    [InlineData(50_000, "mempack5")]
    [InlineData(50_000, "msgpack5")]
    // Fastest options (compact)
    [InlineData(200_000, "mempack5c")]
    [InlineData(200_000, "msgpack5c")]
    [InlineData(200_001, "mempack5c")]
    [InlineData(200_001, "msgpack5c")]
    public async Task PerformanceTest(int iterationCount, string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        RpcFrameDelayerFactory = null;
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var threadCount = Math.Max(1, HardwareInfo.ProcessorCount / 2);
        var tasks = new Task[threadCount];
        await Run(100); // Warmup

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

        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

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
}
