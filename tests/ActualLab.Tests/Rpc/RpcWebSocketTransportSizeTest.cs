using System.Net.WebSockets;
using System.Text.Json;
using ActualLab.Rpc;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.Serialization.Internal;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Tests.Rpc;

public class RpcWebSocketTransportSizeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task FragmentedMessageCrossingLimitIsClosedAsTooLarge()
    {
        var fragments = new[] {
            new Fragment("{}\n"u8.ToArray(), false),
            new Fragment("x"u8.ToArray(), true),
        };
        var options = RpcWebSocketTransport.Options.Default with { MaxMessageSize = 3 };
        var (transport, webSocket, services) = NewTransport(options, fragments);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        (await reader.MoveNextAsync()).Should().BeFalse();
        var closeStatus = await webSocket.WhenClosed.WaitAsync(TimeSpan.FromSeconds(2));

        closeStatus.Should().Be(WebSocketCloseStatus.MessageTooBig);
    }

    [Fact]
    public async Task MessageAtExactLimitIsAccepted()
    {
        var message = "{}\n"u8.ToArray();
        var fragments = new[] {
            new Fragment(message[..2], false),
            new Fragment(message[2..], true),
        };
        var options = RpcWebSocketTransport.Options.Default with { MaxMessageSize = message.Length };
        var (transport, _, services) = NewTransport(options, fragments);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        (await reader.MoveNextAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task MaximumTextEnvelopeAndArgumentPayloadFitDerivedLimit()
    {
        const int maxArgumentDataSize = 1024;
        var escapedMethod = new string('\u0001', RpcTextMessageSerializerV3.MaxMethodRefSize);
        var escapedKey = new string('\u0001', RpcTextMessageSerializerV3.MaxHeaderKeySize);
        var escapedValue = new string('\u0001', RpcTextMessageSerializerV3.MaxHeaderValueSize);
        var headers = Enumerable.Range(0, RpcTextMessageSerializerV3.MaxHeaderCount)
            .SelectMany(_ => new[] { escapedKey, escapedValue })
            .ToList();
        var envelope = JsonSerializer.SerializeToUtf8Bytes(
            new JsonRpcMessage(byte.MaxValue, long.MinValue, escapedMethod, headers));
        var message = new byte[envelope.Length + 1 + maxArgumentDataSize];
        envelope.CopyTo(message, 0);
        message[envelope.Length] = (byte)'\n';
        message.AsSpan(envelope.Length + 1).Fill(1);
        var maxMessageSize = RpcTextMessageSerializerV3.GetMaxMessageSize(maxArgumentDataSize);
        var fragments = new[] { new Fragment(message, true) };
        var options = RpcWebSocketTransport.Options.Default with { MaxMessageSize = maxMessageSize };
        var (transport, _, services) = NewTransport(options, fragments, maxArgumentDataSize);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        RpcWebSocketTransport.Options.Default.MaxMessageSize.Should().Be(
            RpcTextMessageSerializerV3.GetMaxMessageSize(Math.Max(
                RpcTextMessageSerializer.Defaults.MaxArgumentDataSize,
                RpcByteMessageSerializer.Defaults.MaxArgumentDataSize)));
        message.Length.Should().BeLessThanOrEqualTo(maxMessageSize);
        envelope.Length.Should().Be(RpcTextMessageSerializerV3.MaxEnvelopeSize);
        (await reader.MoveNextAsync()).Should().BeTrue();
    }

    private static (RpcWebSocketTransport Transport, FragmentedWebSocket WebSocket, ServiceProvider Services)
        NewTransport(
            RpcWebSocketTransport.Options options,
            IReadOnlyList<Fragment> fragments,
            int? maxArgumentDataSize = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddRpc();
        var format = RpcSerializationFormat.SystemJsonV5;
        if (maxArgumentDataSize is { } maxSize) {
            format = new RpcSerializationFormat(
                "json-size-test",
                () => RpcSerializationFormat.SystemJsonV5.ArgumentSerializer,
                peer => new RpcTextMessageSerializerV3(peer) { MaxArgumentDataSize = maxSize });
            services.AddSingleton(_ => new RpcSerializationFormatResolver(format.Key, new[] { format }));
        }
        var serviceProvider = services.BuildServiceProvider();
        var peerRef = RpcPeerRef.NewClient("size-test", format.Key);
        var peer = new RpcClientPeer(serviceProvider.RpcHub(), peerRef);
        var webSocket = new FragmentedWebSocket(fragments);
        var owner = new WebSocketOwner("size-test", webSocket, serviceProvider);
        var transport = new RpcWebSocketTransport(options, peer, owner) {
            OwnsWebSocketOwner = false,
        };
        return (transport, webSocket, serviceProvider);
    }

    private sealed record Fragment(byte[] Data, bool EndOfMessage);

    private sealed class FragmentedWebSocket(IReadOnlyList<Fragment> fragments) : WebSocket
    {
        private readonly TaskCompletionSource<WebSocketCloseStatus> _whenClosed = new();
        private WebSocketState _state = WebSocketState.Open;
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;
        private int _fragmentIndex;
        private int _fragmentOffset;

        public Task<WebSocketCloseStatus> WhenClosed => _whenClosed.Task;
        public override WebSocketCloseStatus? CloseStatus => _closeStatus;
        public override string? CloseStatusDescription => _closeStatusDescription;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public override void Abort()
            => _state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.Closed;
            _whenClosed.TrySetResult(closeStatus);
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose()
            => Abort();

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (_fragmentIndex >= fragments.Count)
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var fragment = fragments[_fragmentIndex];
            var count = Math.Min(buffer.Count, fragment.Data.Length - _fragmentOffset);
            fragment.Data.AsSpan(_fragmentOffset, count).CopyTo(buffer.AsSpan());
            _fragmentOffset += count;
            var isFragmentComplete = _fragmentOffset == fragment.Data.Length;
            var endOfMessage = isFragmentComplete && fragment.EndOfMessage;
            if (isFragmentComplete) {
                _fragmentIndex++;
                _fragmentOffset = 0;
            }
            return Task.FromResult(new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
