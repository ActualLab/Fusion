using ActualLab.Net;
using ActualLab.Rpc;
using ActualLab.Rpc.Internal;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Net;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConnectorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public static async Task BasicTest()
    {
        var clock = CpuClock.Instance;
        var connectionDelay = TimeSpan.FromSeconds(0.25);
        var disconnectedUntil = default(Moment);

        var c = new Connector<Connection>(async ct => {
            // ReSharper disable once AccessToModifiedClosure
            if (clock.Now <= disconnectedUntil)
                throw RpcDisconnectedException.New("server");
            await Task.Delay(connectionDelay, ct).ConfigureAwait(false);
            return new Connection();
        }) {
            ReconnectDelayer = new RetryDelayer() { Delays = RetryDelaySeq.Exp(0.25, 2, 0, 2) }
        };

        var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => c.GetConnection(cts.Token));
        c.IsConnected.Value.Value.Should().BeFalse();

        var c1 = await c.GetConnection(CancellationToken.None);
        c1.Should().NotBeNull();
        await c.IsConnected
            .When(x => x.ValueOrDefault, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.2), CancellationToken.None);

        c.DropConnection(c1, new RpcDisconnectedException());
        await c.IsConnected
            .When(x => x.Error is RpcDisconnectedException, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.1), CancellationToken.None);
        await c.IsConnected
            .When(x => x.ValueOrDefault, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.7), CancellationToken.None);
        var c2 = await c.GetConnection(CancellationToken.None);
        c2.Should().NotBeSameAs(c1);

        disconnectedUntil = clock.Now + TimeSpan.FromSeconds(0.4); // 2 retry delays
        c.DropConnection(c2, new RpcDisconnectedException());
        await c.IsConnected
            .When(x => x.Error is RpcDisconnectedException, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.1), CancellationToken.None);
        await c.IsConnected
            .When(x => x.ValueOrDefault, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1) + connectionDelay, CancellationToken.None);
        var c3 = await c.GetConnection(CancellationToken.None);
        c3.Should().NotBeSameAs(c2);

        c.DropConnection(c3, new RpcDisconnectedException());
        await c.IsConnected
            .When(x => x.Error is RpcDisconnectedException, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.1), CancellationToken.None);
        await c.IsConnected
            .When(x => x.ValueOrDefault, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(0.7), CancellationToken.None);
        var c4 = await c.GetConnection(CancellationToken.None);
        c4.Should().NotBeSameAs(c3);
    }

    // Nested types

    public class Connection
    {
        private static int _lastId;

        public int Id { get; }

        public Connection()
            => Id = Interlocked.Increment(ref _lastId);
    }
}
