using ActualLab.Collections;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcKeepAliveTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static readonly TimeSpan KeepAlivePeriod = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KeepAliveCheckPeriod = TimeSpan.FromSeconds(1);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        commander.AddHandlers<TestRpcService>();
    }

    [Fact(Timeout = 30_000)]
    public async Task KeepsConnectionAliveWhileKeepAliveFlows()
    {
        await using var services = CreateServices(UseKeepAliveLimits());
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2); // Warm-up + ensures connected

        var connectedState = clientPeer.ConnectionState.Value;
        connectedState.IsConnected().Should().BeTrue();
        var keepAliveAt0 = clientPeer.LastKeepAliveAt;

        // Stay idle well past KeepAliveTimeout. If keep-alives stopped flowing the
        // watchdog in RpcSharedObjectTracker.Maintain would drop the connection.
        await Delay((KeepAliveTimeout + KeepAlivePeriod).TotalSeconds);

        connectedState.WhenDisconnected.IsCompleted.Should().BeFalse();
        clientPeer.ConnectionState.Value.Should().BeSameAs(connectedState);
        // Keep-alives actually arrived, so LastKeepAliveAt advanced past the warm-up value.
        (clientPeer.LastKeepAliveAt - keepAliveAt0).Should().BeGreaterThan(TimeSpan.Zero);
        (await client.Add(2, 3)).Should().Be(5);
    }

    [Fact(Timeout = 30_000)]
    public async Task DropsConnectionWhenKeepAliveStops()
    {
        var gate = new ConnectionGate();
        await using var services = CreateServices(UseKeepAliveLimits(gate));
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2); // Warm-up + ensures connected

        var connectedState = clientPeer.ConnectionState.Value;
        connectedState.IsConnected().Should().BeTrue();

        // Drop every server->client frame (incl. $sys.KeepAlive) while leaving the
        // channel open: a half-open link the keep-alive watchdog must detect.
        gate.IsClosed = true;
        var lastKeepAliveAt = clientPeer.LastKeepAliveAt;

        var maxWait = KeepAliveTimeout + KeepAliveCheckPeriod + TimeSpan.FromSeconds(4);
        await connectedState.WhenDisconnected.WaitAsync(maxWait);

        // The drop must be the keep-alive timeout: it can't fire until LastKeepAliveAt
        // is older than KeepAliveTimeout, and it's detected within one check period.
        var staleness = Moment.Now - lastKeepAliveAt;
        staleness.Should().BeGreaterThan(KeepAliveTimeout - KeepAliveCheckPeriod);
        staleness.Should().BeLessThan(KeepAliveTimeout + KeepAliveCheckPeriod + TimeSpan.FromSeconds(3));
    }

    // Private methods

    private static Action<IServiceCollection> UseKeepAliveLimits(ConnectionGate? gate = null)
        => services => {
            services.AddSingleton(_ => new RpcLimits(false) {
                KeepAlivePeriod = KeepAlivePeriod,
                KeepAliveTimeout = KeepAliveTimeout,
                ObjectReleasePeriod = KeepAliveCheckPeriod,
            });
            if (gate is not null)
                services.AddSingleton(_ => RpcTestClientOptions.Default with {
                    ConnectionFactory = testClient => CreateGatedConnection(testClient, gate),
                });
        };

    private static ChannelPair<ArrayOwner<byte>> CreateGatedConnection(RpcTestClient testClient, ConnectionGate gate)
    {
        var channelOptions = testClient.Options.ChannelOptions;
        var channel1 = ChannelExt.Create<ArrayOwner<byte>>(channelOptions);
        var channel2 = ChannelExt.Create<ArrayOwner<byte>>(channelOptions);
        // Twisted exactly like the default factory, except the server's outbound writer
        // (channel1, which the client reads) is wrapped so the gate can drop its frames.
        var serverWriter = new GatedChannelWriter(channel1.Writer, gate);
        return new ChannelPair<ArrayOwner<byte>>(
            new CustomChannel<ArrayOwner<byte>>(channel1.Reader, channel2.Writer),
            new CustomChannel<ArrayOwner<byte>>(channel2.Reader, serverWriter));
    }

    // Nested types

    private sealed class ConnectionGate
    {
        public volatile bool IsClosed;
    }

    private sealed class GatedChannelWriter(ChannelWriter<ArrayOwner<byte>> inner, ConnectionGate gate)
        : ChannelWriter<ArrayOwner<byte>>
    {
        public override bool TryWrite(ArrayOwner<byte> item)
        {
            if (!gate.IsClosed)
                return inner.TryWrite(item);

            item.Dispose(); // Return the pooled buffer instead of leaking it
            return true;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
            => inner.WaitToWriteAsync(cancellationToken);

        public override bool TryComplete(Exception? error = null)
            => inner.TryComplete(error);
    }
}
