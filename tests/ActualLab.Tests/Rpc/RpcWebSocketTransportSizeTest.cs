using System.Net.WebSockets;
using System.Text.Json;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
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
        var options = RpcWebSocketTransport.Options.Default with {
            MaxMessageSize = maxMessageSize,
            MaxPreHandshakeMessageSize = maxMessageSize,
        };
        var (transport, _, services) = NewTransport(options, fragments, maxArgumentDataSize);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        message.Length.Should().BeLessThanOrEqualTo(maxMessageSize);
        envelope.Length.Should().Be(RpcTextMessageSerializerV3.MaxEnvelopeSize);
        (await reader.MoveNextAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task PreHandshakeMessageOverCapIsRejectedEarly()
    {
        const int preHandshakeLimit = 4096;
        var options = RpcWebSocketTransport.Options.Default with {
            MaxMessageSize = 1 << 20,
            MaxPreHandshakeMessageSize = preHandshakeLimit,
        };
        var fragments = Enumerable.Range(0, 1 << 20 >> 10)
            .Select(_ => new Fragment(new byte[1024], false))
            .ToArray();
        var (transport, webSocket, services) = NewTransport(options, fragments);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        (await reader.MoveNextAsync()).Should().BeFalse();
        (await webSocket.WhenClosed.WaitAsync(TimeSpan.FromSeconds(2)))
            .Should().Be(WebSocketCloseStatus.MessageTooBig);
        webSocket.ReceivedByteCount.Should().BeLessThanOrEqualTo(preHandshakeLimit);
    }

    [Fact]
    public async Task OverLimitMessageIsRejectedWithoutAnExtraRead()
    {
        const int limit = 8192;
        var options = RpcWebSocketTransport.Options.Default with {
            MaxMessageSize = limit,
            MaxPreHandshakeMessageSize = limit,
        };
        // A single unterminated fragment that fills the limit exactly - and nothing after it
        var fragments = new[] { new Fragment(new byte[limit], false) };
        var (transport, webSocket, services) = NewTransport(options, fragments);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        (await reader.MoveNextAsync()).Should().BeFalse();
        (await webSocket.WhenClosed.WaitAsync(TimeSpan.FromSeconds(2)))
            .Should().Be(WebSocketCloseStatus.MessageTooBig);
        webSocket.ReceivedByteCount.Should().Be(limit);
    }

    [Fact]
    public async Task PostHandshakeMessageUsesTheNormalLimit()
    {
        const int preHandshakeLimit = 16;
        var tail = new byte[1024];
        tail.AsSpan().Fill((byte)' ');
        tail[0] = (byte)'\n';
        var options = RpcWebSocketTransport.Options.Default with {
            MaxMessageSize = 1 << 20,
            MaxPreHandshakeMessageSize = preHandshakeLimit,
        };
        var fragments = new[] {
            new Fragment("{}\n"u8.ToArray(), true), // "Handshake"
            new Fragment("{}"u8.ToArray(), false),
            new Fragment(tail, true), // Way above preHandshakeLimit
        };
        var (transport, _, services) = NewTransport(options, fragments);
        await using var _1 = services;
        await using var _2 = transport;
        await using var reader = transport.GetAsyncEnumerator();

        (await reader.MoveNextAsync()).Should().BeTrue();
        (await reader.MoveNextAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task HandshakeFitsPreHandshakeLimitInEveryFormat()
    {
        var apiVersions = Enumerable.Range(0, 32)
            .ToDictionary(i => $"ActualLab.Rpc.Tests.SampleService{i}", _ => Version.Parse("1.2.3.4"));
        var handshake = new RpcHandshake(
            Guid.NewGuid(), new VersionSet(apiVersions), Guid.NewGuid(),
            RpcHandshake.CurrentProtocolVersion, 1);
        var maxSize = 0;
        foreach (var format in RpcSerializationFormat.All) {
            var (transport, webSocket, services) = NewTransport(
                RpcWebSocketTransport.Options.Default, [], format: format);
            await using var _1 = services;
            await using var _2 = transport;
            services.GetRequiredService<RpcSystemCallSender>()
                .Handshake(transport.Peer, transport, handshake);
            transport.TryComplete();
            await transport.WhenClosed.WaitAsync(TimeSpan.FromSeconds(5));

            var size = webSocket.SentByteCount;
            Out.WriteLine($"{format.Key}: {size} bytes");
            maxSize = Math.Max(maxSize, size);
        }

        maxSize.Should().BeLessThan(RpcFrameBasedTransport.DefaultMaxPreHandshakeFrameSize / 2);
    }

    [Fact]
    public async Task MaxArgumentDataSizeMessageFitsMaxFrameSizeInEveryFormat()
    {
        var maxFrameSize = RpcFrameBasedTransport.DefaultMaxFrameSize;
        var maxArgumentDataSize = Math.Max(
            RpcTextMessageSerializer.Defaults.MaxArgumentDataSize,
            RpcByteMessageSerializer.Defaults.MaxArgumentDataSize);
        RpcWebSocketTransport.Options.Default.MaxMessageSize.Should().Be(maxFrameSize);
        RpcPipeTransport.Options.Default.MaxFrameSize.Should().Be(maxFrameSize);
        RpcStreamTransport.Options.Default.MaxFrameSize.Should().Be(maxFrameSize);
        // The worst-case envelope of the most expensive registered format must still fit
        RpcTextMessageSerializerV3.GetMaxMessageSize(maxArgumentDataSize)
            .Should().BeLessThanOrEqualTo(maxFrameSize);

        var argumentData = new byte[maxArgumentDataSize];
        argumentData.AsSpan().Fill((byte)'x');
        var options = RpcWebSocketTransport.Options.Default with {
            MaxPreHandshakeMessageSize = maxFrameSize,
        };
        foreach (var format in RpcSerializationFormat.All) {
            var (transport, webSocket, services) = NewTransport(options, [], format: format);
            await using (services)
            await using (transport)
                (await SendAndClose(transport, services, argumentData)).Should().BeNull();

            var frame = webSocket.LastSentFrame!;
            Out.WriteLine($"{format.Key}: {frame.Length} bytes");
            frame.Length.Should().BeLessThanOrEqualTo(maxFrameSize);

            var (reReader, _, reReaderServices) =
                NewTransport(options, [new Fragment(frame, true)], format: format);
            await using (reReaderServices)
            await using (reReader) {
                await using var reader = reReader.GetAsyncEnumerator();
                (await reader.MoveNextAsync()).Should().BeTrue();
                reader.Current.ArgumentData.Length.Should().Be(maxArgumentDataSize);
            }
        }
    }

    [Fact]
    public async Task OversizedOutboundMessageFailsLocally()
    {
        var options = RpcWebSocketTransport.Options.Default with {
            FrameSize = 1024,
            MaxMessageSize = 4096,
        };
        var (transport, webSocket, services) = NewTransport(options, []);
        await using var _1 = services;
        await using var _2 = transport;

        var error = await SendAndClose(transport, services, new byte[8192]);

        error.Should().BeOfType<FormatException>();
        webSocket.SentByteCount.Should().Be(0);
    }

    private static async Task<Exception?> SendAndClose(
        RpcWebSocketTransport transport,
        IServiceProvider services,
        ReadOnlyMemory<byte> argumentData)
    {
        var methodDef = services.GetRequiredService<RpcSystemCallSender>().OkMethodDef;
        var whenSent = TaskCompletionSourceExt.New<Exception?>();
        var message = new RpcOutboundMessage(
            new RpcOutboundContext(transport.Peer), methodDef, 1, false, null, argumentData,
            (_, _, error) => whenSent.TrySetResult(error));
        transport.Send(message);
        transport.TryComplete();
        await transport.WhenClosed.WaitAsync(TimeSpan.FromSeconds(30));
        return await whenSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static (RpcWebSocketTransport Transport, FragmentedWebSocket WebSocket, ServiceProvider Services)
        NewTransport(
            RpcWebSocketTransport.Options options,
            IReadOnlyList<Fragment> fragments,
            int? maxArgumentDataSize = null,
            RpcSerializationFormat? format = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddRpc();
        var actualFormat = format ?? RpcSerializationFormat.SystemJsonV5;
        if (maxArgumentDataSize is { } maxSize) {
            actualFormat = new RpcSerializationFormat(
                "json-size-test",
                () => RpcSerializationFormat.SystemJsonV5.ArgumentSerializer,
                peer => new RpcTextMessageSerializerV3(peer) { MaxArgumentDataSize = maxSize });
            services.AddSingleton(
                _ => new RpcSerializationFormatResolver(actualFormat.Key, new[] { actualFormat }));
        }
        var serviceProvider = services.BuildServiceProvider();
        var rpcRef = RpcRef.NewClient("size-test", actualFormat.Key);
        var peer = new RpcClientPeer(serviceProvider.RpcHub(), rpcRef.Route);
        var messageType = peer.MessageSerializer is RpcTextMessageSerializer
            ? WebSocketMessageType.Text
            : WebSocketMessageType.Binary;
        var webSocket = new FragmentedWebSocket(fragments, messageType);
        var owner = new WebSocketOwner("size-test", webSocket, serviceProvider);
        var transport = new RpcWebSocketTransport(options, peer, owner) {
            OwnsWebSocketOwner = false,
        };
        return (transport, webSocket, serviceProvider);
    }

    private sealed record Fragment(byte[] Data, bool EndOfMessage);

    private sealed class FragmentedWebSocket(
        IReadOnlyList<Fragment> fragments,
        WebSocketMessageType messageType = WebSocketMessageType.Text)
        : WebSocket
    {
        private readonly TaskCompletionSource<WebSocketCloseStatus> _whenClosed = new();
        private WebSocketState _state = WebSocketState.Open;
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;
        private int _fragmentIndex;
        private int _fragmentOffset;

        public int ReceivedByteCount;
        public int ReceiveCallCount;
        public int SentByteCount;
        public byte[]? LastSentFrame;
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
            ReceiveCallCount++;
            if (_fragmentIndex >= fragments.Count)
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var fragment = fragments[_fragmentIndex];
            var count = Math.Min(buffer.Count, fragment.Data.Length - _fragmentOffset);
            fragment.Data.AsSpan(_fragmentOffset, count).CopyTo(buffer.AsSpan());
            ReceivedByteCount += count;
            _fragmentOffset += count;
            var isFragmentComplete = _fragmentOffset == fragment.Data.Length;
            var endOfMessage = isFragmentComplete && fragment.EndOfMessage;
            if (isFragmentComplete) {
                _fragmentIndex++;
                _fragmentOffset = 0;
            }
            return Task.FromResult(new WebSocketReceiveResult(count, messageType, endOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            LastSentFrame = buffer.ToArray();
            Interlocked.Add(ref SentByteCount, buffer.Count);
            return Task.CompletedTask;
        }
    }
}
