using System.Net.WebSockets;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// Reproduces the WASM "cannot reconnect" symptom: when <see cref="WebSocketOwner.ConnectAsync"/>
/// hangs past <c>RpcLimits.ConnectTimeout</c> without honoring its cancellation token (the
/// real-world Browser/WASM behavior we observed), the <see cref="WebSocketOwner"/> created
/// inside <c>RpcWebSocketClient.ConnectRemote</c> must still be disposed; otherwise each
/// timed-out reconnect leaks a <see cref="ClientWebSocket"/> in CONNECTING state and the
/// origin eventually exhausts its connection budget.
/// </summary>
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketClientConnectLeakTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public Task ConnectTimeout_DisposesHungWebSocketOwner()
        => RunReconnectLoop(ignoreCancellation: true);

    [Fact]
    public Task ConnectTimeout_DisposesOwnerEvenWhenCancellationIsHonored()
    {
        // Sanity test: when ConnectAsync honors cancellation, every owner is still disposed.
        // Verifies the fix doesn't regress the happy-path cleanup that already existed.
        return RunReconnectLoop(ignoreCancellation: false);
    }

    // Private methods

    private async Task RunReconnectLoop(bool ignoreCancellation)
    {
        var created = new ConcurrentBag<HangingWebSocketOwner>();
        var sp = BuildServices(created, ignoreCancellation);
        await using var _ = sp as IAsyncDisposable;

        var hub = sp.RpcHub();
        // Touching the peer starts its OnRun loop, which calls ConnectRemote repeatedly.
        var peer = hub.GetClientPeer(RpcRef.Default);

        // Let several reconnect attempts fire. ConnectTimeout is 200 ms and the reconnect
        // delay starts at ~1 s for clients, so 3 s is enough for at least 2 cycles.
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Stop the peer so we don't race with new owners after taking the snapshot.
        await peer.DisposeAsync();

        var snapshot = created.ToArray();
        Out.WriteLine($"Created {snapshot.Length} WebSocketOwner(s) across reconnect attempts.");
        snapshot.Length.Should().BeGreaterThan(0, "the peer's OnRun loop should have invoked ConnectRemote at least once");

        // Wait briefly so any fire-and-forget orphan-dispose tasks complete.
        await TestExt.When(() => {
            var leaked = snapshot.Count(o => !o.IsDisposed);
            leaked.Should().Be(0,
                "every WebSocketOwner created by ConnectRemote must be disposed after ConnectTimeout, "
                + $"but {leaked} of {snapshot.Length} were leaked");
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(3));
    }

    private IServiceProvider BuildServices(ConcurrentBag<HangingWebSocketOwner> created, bool ignoreCancellation)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var rpc = services.AddRpc();
        rpc.AddWebSocketClient(_ => new RpcWebSocketClientOptions {
            HostUrlResolver = _ => "ws://localhost:1",
            ConnectionUriResolver = _ => new Uri("ws://localhost:1/rpc/ws"),
            WebSocketOwnerFactory = peer => {
                var owner = new HangingWebSocketOwner(peer.Hub.Services, ignoreCancellation);
                created.Add(owner);
                return owner;
            },
        });

        // Override RpcLimits AFTER AddRpc so our short ConnectTimeout wins.
        services.AddSingleton(_ => new RpcLimits(useDebugDefaults: false) {
            ConnectTimeout = TimeSpan.FromMilliseconds(200),
        });

        return services.BuildServiceProvider();
    }

    private sealed class HangingWebSocketOwner(IServiceProvider services, bool ignoreCancellation)
        : WebSocketOwner("test", new ClientWebSocket(), services)
    {
        public override Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
            => ignoreCancellation
                ? TaskExt.NeverEnding(CancellationToken.None) // Real WASM scenario: token isn't honored
                : Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Honors cancellation
    }
}
