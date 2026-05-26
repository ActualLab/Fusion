using System.Net.WebSockets;
using ActualLab.Rpc;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketTransportCancellationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task ReadCancellationAbortsWebSocketReceive()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddRpc();
        using var sp = services.BuildServiceProvider();

        var peer = new RpcClientPeer(sp.RpcHub(), RpcPeerRef.Default);
        var webSocket = new AbortableReceiveWebSocket();
        var webSocketOwner = new WebSocketOwner("test", webSocket, sp);
        var transport = new RpcWebSocketTransport(RpcWebSocketTransport.Options.Default, peer, webSocketOwner);
        await using var _ = transport;

        using var cts = new CancellationTokenSource();
        await using var reader = transport.GetAsyncEnumerator(cts.Token);

        var moveNextTask = reader.MoveNextAsync().AsTask();
        await webSocket.WhenReceiveStarted.WaitAsync(TimeSpan.FromSeconds(1));

        cts.Cancel();

        (await moveNextTask.WaitAsync(TimeSpan.FromSeconds(1))).Should().BeFalse();
        webSocket.AbortCount.Should().BeGreaterThan(0);
    }

    private sealed class AbortableReceiveWebSocket : WebSocket
    {
        private readonly TaskCompletionSource<Unit> _whenReceiveStarted = TaskCompletionSourceExt.New<Unit>();
        private readonly TaskCompletionSource<Unit> _whenAborted = TaskCompletionSourceExt.New<Unit>();
        private volatile WebSocketState _state = WebSocketState.Open;
        private int _abortCount;

        public Task WhenReceiveStarted => _whenReceiveStarted.Task;
        public int AbortCount => Volatile.Read(ref _abortCount);
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
            Interlocked.Increment(ref _abortCount);
            _whenAborted.TrySetResult(default);
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            _whenAborted.TrySetResult(default);
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose()
            => Abort();

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            _whenReceiveStarted.TrySetResult(default);
            await _whenAborted.Task.ConfigureAwait(false);
            throw new ObjectDisposedException(nameof(AbortableReceiveWebSocket));
        }

        public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            _whenReceiveStarted.TrySetResult(default);
            await _whenAborted.Task.ConfigureAwait(false);
            throw new ObjectDisposedException(nameof(AbortableReceiveWebSocket));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public override ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
