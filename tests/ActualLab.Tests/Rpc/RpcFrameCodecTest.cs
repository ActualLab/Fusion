using System.Diagnostics.Metrics;
using System.Text;
using ActualLab.IO.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Tests.Rpc;

public class RpcFrameCodecTest(ITestOutputHelper @out) : TestBase(@out)
{
    // Frame-based transports reserve this many bytes for the frame length prefix before
    // serializing the first message, and strip them again when the frame is sent.
    private const int FrameHeaderSize = sizeof(int);

    [Theory]
    [InlineData("json5")]
    [InlineData("json5np")]
    public void TextFrameCarriesOnlyRealMessages(string formatKey)
    {
        var (payload, expectedArgs) = WriteFrame(formatKey, 3);

        payload[0].Should().NotBe((byte)'\n',
            "a frame must start with its first message, not with a message delimiter");

        var (messages, skipped) = ReadFrame(formatKey, payload);

        skipped.Should().Be(0, "every segment in the frame must be a real message");
        messages.Count.Should().Be(3);
        for (var i = 0; i < messages.Count; i++)
            messages[i].ArgumentData.ToArray().Should().Equal(expectedArgs[i],
                $"argument data of message {i} must round-trip byte-for-byte");
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("json5np")]
    public void TextFrameFromLegacyWriterIsStillReadable(string formatKey)
    {
        // Fusion <= 14.2.39 prefixed the frame's first message with a LF+RS delimiter,
        // because it decided "am I first?" from a buffer offset that the reserved frame
        // header had already pushed past zero.
        var (payload, expectedArgs) = WriteFrame(formatKey, 2);
        var legacyPayload = new byte[payload.Length + 2];
        legacyPayload[0] = (byte)'\n';
        legacyPayload[1] = 0x1E;
        payload.CopyTo(legacyPayload, 2);

        var (messages, skipped) = ReadFrame(formatKey, legacyPayload);

        skipped.Should().Be(0, "the leading delimiter must be skipped, not parsed as a message");
        messages.Count.Should().Be(2);
        messages[0].ArgumentData.ToArray().Should().Equal(expectedArgs[0]);
    }

    // Private methods

    private static (byte[] Payload, List<byte[]> ExpectedArgs) WriteFrame(string formatKey, int messageCount)
    {
        var (peer, services) = NewPeer(formatKey);
        using var _ = services;
        var codec = NewCodec(peer);
        var methodDef = services.GetRequiredService<RpcSystemCallSender>().OkMethodDef;

        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, 16384, mustClear: false);
        buffer.Advance(FrameHeaderSize);
        var expectedArgs = new List<byte[]>();
        for (var i = 0; i < messageCount; i++) {
            var args = Encoding.UTF8.GetBytes($"\"args-{i}\"");
            expectedArgs.Add(args);
            codec.Serialize(
                new RpcOutboundMessage(new RpcOutboundContext(peer), methodDef, i + 1, false, null, args),
                buffer);
        }
        return (buffer.WrittenMemory[FrameHeaderSize..].ToArray(), expectedArgs);
    }

    private static (List<RpcInboundMessage> Messages, int Skipped) ReadFrame(string formatKey, byte[] payload)
    {
        var (peer, services) = NewPeer(formatKey);
        using var _ = services;
        var codec = NewCodec(peer);

        var messages = new List<RpcInboundMessage>();
        var skipped = 0;
        var offset = 0;
        while (offset < payload.Length) {
            var startOffset = offset;
            var message = codec.TryDeserialize(payload, ref offset, payload.Length);
            if (message is not null)
                messages.Add(message);
            else
                skipped++;
            offset.Should().BeGreaterThan(startOffset, "TryDeserialize must always make progress");
        }
        return (messages, skipped);
    }

    private static RpcFrameCodec NewCodec(RpcPeer peer)
    {
        var meter = new Meter($"frame-codec-test-{Guid.NewGuid():N}");
        return new RpcFrameCodec(
            peer.MessageSerializer,
            meter.CreateCounter<long>("in"),
            meter.CreateCounter<long>("out"),
            null,
            FrameHeaderSize);
    }

    private static (RpcClientPeer Peer, ServiceProvider Services) NewPeer(string formatKey)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddRpc();
        var serviceProvider = services.BuildServiceProvider();
        var rpcRef = RpcRef.NewClient("frame-codec-test", formatKey);
        return (new RpcClientPeer(serviceProvider.RpcHub(), rpcRef.Route), serviceProvider);
    }
}
