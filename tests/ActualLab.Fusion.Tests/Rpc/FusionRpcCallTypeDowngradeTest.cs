using ActualLab.Channels;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Tests;
using ActualLab.Testing.Collections;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Fusion.Tests.Rpc;

// Server-side compute service.
public interface ICallTypeDowngradeCounter : IComputeService
{
    [ComputeMethod]
    public Task<int> Get(string key, CancellationToken cancellationToken = default);
    public Task Set(string key, int value, CancellationToken cancellationToken = default);
}

// Client-side mirror — same wire shape, but a regular RPC service.
// Registered under the server interface's wire name to map to the same server method,
// and lacking [ComputeMethod] so its outbound calls go out as Regular (CallTypeId = 0).
public interface ICallTypeDowngradeCounterClient : IRpcService
{
    public Task<int> Get(string key, CancellationToken cancellationToken = default);
    public Task Set(string key, int value, CancellationToken cancellationToken = default);
}

public class CallTypeDowngradeCounter : ICallTypeDowngradeCounter
{
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_counters.TryGetValue(key, out var v) ? v : 0);

    public Task Set(string key, int value, CancellationToken cancellationToken = default)
    {
        _counters[key] = value;
        using (Invalidation.Begin())
            _ = Get(key, default).AssertCompleted();
        return Task.CompletedTask;
    }
}

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcCallTypeDowngradeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task RegularCallToComputeMethodTest()
    {
        await using var serverServices = BuildServerServices();
        var bridge = new CrossHubRpcClient.Bridge(serverServices.RpcHub());
        await using var clientServices = BuildClientServices(bridge);

        var client = clientServices.RpcHub().GetClient<ICallTypeDowngradeCounterClient>();
        await client.Set("a", 42);
        (await client.Get("a")).Should().Be(42);
        (await client.Get("b")).Should().Be(0);

        // Server must not retain the call after responding (no Stage 2 invalidation hold).
        var serverPeer = bridge.ServerPeer ?? throw new InvalidOperationException("No server peer was created.");
        for (var i = 0; i < 50 && serverPeer.InboundCalls.Count > 0; i++)
            await Task.Delay(20);
        serverPeer.InboundCalls.Count.Should().Be(0);
    }

    // Private methods

    private ServiceProvider BuildServerServices()
    {
        var services = new ServiceCollection();
        AddCommonServices(services);
        var fusion = services.AddFusion();
        fusion.AddServer<ICallTypeDowngradeCounter, CallTypeDowngradeCounter>();
        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildClientServices(CrossHubRpcClient.Bridge bridge)
    {
        var services = new ServiceCollection();
        AddCommonServices(services);
        var rpc = services.AddRpc();
        // Client uses a different interface, but registered under the server interface's wire name.
        rpc.AddClient<ICallTypeDowngradeCounterClient>(nameof(ICallTypeDowngradeCounter));
        services.AddSingleton<RpcClient>(c => new CrossHubRpcClient(c, bridge));
        return services.BuildServiceProvider();
    }

    private void AddCommonServices(IServiceCollection services)
    {
        services.AddSingleton(Out);
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(
#pragma warning disable CS0618
                new XunitTestOutputLoggerProvider(
                    new TestOutputHelperAccessor() { Output = Out },
                    (_, level) => level >= LogLevel.Debug));
#pragma warning restore CS0618
        });
        services.AddRpc();
    }

    // RpcClient that bridges a client peer in this hub to a server peer in another hub
    // via an in-memory channel pair. Mirrors RpcClient.ConnectLoopback but cross-hub.
    private sealed class CrossHubRpcClient(IServiceProvider services, CrossHubRpcClient.Bridge bridge)
        : RpcClient(services)
    {
        public sealed class Bridge(RpcHub serverHub)
        {
            public RpcHub ServerHub { get; } = serverHub;
            public RpcServerPeer? ServerPeer { get; set; }
        }

        public override async Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
        {
            var serverPeerRef = RpcPeerRef.NewServer(
                clientPeer.ClientId,
                clientPeer.SerializationFormat.Key,
                clientPeer.Ref.IsBackend);
            var serverPeer = bridge.ServerHub.GetServerPeer(serverPeerRef);
            bridge.ServerPeer = serverPeer;
            var channelPair = ChannelPair.CreateTwisted<ArrayOwner<byte>>(LocalChannelOptions);

            var clientTransport = new RpcSimpleChannelTransport(clientPeer, channelPair.Channel1);
            var clientConnection = new RpcConnection(clientTransport);

            var serverTransport = new RpcSimpleChannelTransport(serverPeer, channelPair.Channel2);
            var serverConnection = new RpcConnection(serverTransport);

            await serverPeer.SetNextConnection(serverConnection, cancellationToken).ConfigureAwait(false);
            return clientConnection;
        }
    }
}
