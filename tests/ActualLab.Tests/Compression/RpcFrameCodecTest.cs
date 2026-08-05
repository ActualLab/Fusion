using ActualLab.Compression;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Tests.Compression;

// The frame-level contract: [int32 length|flags][body], a stored path for small frames that costs
// no bytes and no copy, and a context reset the decoder follows without any policy of its own.
public class RpcFrameCodecTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const int Int32Size = sizeof(int);
    private const int MaxFrameSize = 1 << 20;

    [Fact]
    public void RoundTripTest()
    {
        using var encoder = NewEncoder();
        using var decoder = NewDecoder();

        for (var i = 0; i < 20; i++) {
            var payload = NewPayload(i, 500);
            Decode(decoder, Encode(encoder, payload)).Should().Equal(payload);
        }
    }

    // A stored frame is the pre-compression frame, verbatim: same length, no extra byte
    [Fact]
    public void SmallFramesAreStoredTest()
    {
        using var encoder = NewEncoder();
        using var decoder = NewDecoder();
        var payload = NewPayload(0, RpcCompressionOptions.Default.MinCompressedFrameSize - 1);

        var frame = Encode(encoder, payload);
        var header = frame.AsSpan().ReadLittleEndian();
        (header & RpcFrameCodec.CompressedFrameFlag).Should().Be(0);
        (header & RpcFrameCodec.FrameLengthMask).Should().Be(payload.Length);
        (frame.Length - Int32Size).Should().Be(payload.Length);
        Decode(decoder, frame).Should().Equal(payload);
    }

    [Fact]
    public void LargeFramesAreCompressedTest()
    {
        using var encoder = NewEncoder();
        using var decoder = NewDecoder();
        var payload = NewPayload(0, 4000);

        var frame = Encode(encoder, payload);
        var header = frame.AsSpan().ReadLittleEndian();
        (header & RpcFrameCodec.CompressedFrameFlag).Should().Be(RpcFrameCodec.CompressedFrameFlag);
        (header & RpcFrameCodec.FrameLengthMask).Should().Be(frame.Length - Int32Size);
        frame.Length.Should().BeLessThan(payload.Length);
        Decode(decoder, frame).Should().Equal(payload);
    }

    // Mixing stored and compressed frames must not desynchronize the pair: a stored frame is
    // deliberately never fed to either codec.
    [Fact]
    public void MixedStoredAndCompressedTest()
    {
        using var encoder = NewEncoder();
        using var decoder = NewDecoder();

        for (var i = 0; i < 30; i++) {
            var payload = NewPayload(i, i % 2 == 0 ? 20 : 3000);
            Decode(decoder, Encode(encoder, payload)).Should().Equal(payload);
        }
    }

    // The reset is driven by the sender and signalled per frame, so the receiver follows it
    // without any policy of its own.
    [Fact]
    public void ContextResetTest()
    {
        using var encoder = NewEncoder(RpcCompressionOptions.Default with { ContextResetFrames = 4 });
        using var decoder = NewDecoder();

        var resetCount = 0;
        for (var i = 0; i < 30; i++) {
            var payload = NewPayload(i, 2000);
            var frame = Encode(encoder, payload);
            if ((frame.AsSpan().ReadLittleEndian() & RpcFrameCodec.ContextResetFrameFlag) != 0)
                resetCount++;
            Decode(decoder, frame).Should().Equal(payload);
        }

        resetCount.Should().BeGreaterThan(4);
    }

    // What WebSocket transmits: only the header's most significant byte, which carries both flags
    // and - below 16 MiB - no length bits at all.
    [Fact]
    public void HighByteCarriesTheFlagsTest()
    {
        using var encoder = NewEncoder();
        using var decoder = NewDecoder();
        var payload = NewPayload(0, 4000);
        var frame = Encode(encoder, payload);

        var highByte = frame[Int32Size - 1];
        (highByte << 24).Should().Be(RpcFrameCodec.CompressedFrameFlag);
        // Decoding off the high byte alone must give the same result as off the full header
        var (array, offset, end) = decoder.Decode(
            highByte << 24, frame, Int32Size, frame.Length, MaxFrameSize);
        array.AsSpan(offset, end - offset).ToArray().Should().Equal(payload);
    }

    [Fact]
    public void FrameSizeLimitIsUnderTheFlagBitsTest()
    {
        RpcFrameCodec.MaxFrameSize.Should().Be(0x3FFF_FFFF);
        RpcFrameCodec.FrameLengthMask.Should().Be(RpcFrameCodec.MaxFrameSize);
        // The two flags must not overlap the length bits, nor each other
        (RpcFrameCodec.CompressedFrameFlag & RpcFrameCodec.FrameLengthMask).Should().Be(0);
        (RpcFrameCodec.ContextResetFrameFlag & RpcFrameCodec.FrameLengthMask).Should().Be(0);
        (RpcFrameCodec.CompressedFrameFlag & RpcFrameCodec.ContextResetFrameFlag).Should().Be(0);
        // ...and the default transport limit stays well under them
        RpcFrameBasedTransport.DefaultMaxFrameSize.Should().BeLessThanOrEqualTo(RpcFrameCodec.MaxFrameSize);
    }

    // Private methods

    private static RpcFrameEncoder NewEncoder(RpcCompressionOptions? settings = null)
        => new(new LZ4ByteCompressor(), settings ?? RpcCompressionOptions.Default, 1024, MaxFrameSize);

    private static RpcFrameDecoder NewDecoder()
        => new(new LZ4ByteDecompressor(), 1024, MaxFrameSize);

    private static byte[] Encode(RpcFrameEncoder encoder, byte[] payload)
    {
        var frame = new byte[Int32Size + payload.Length];
        payload.CopyTo(frame, Int32Size);
        return encoder.Encode(frame).ToArray();
    }

    private static byte[] Decode(RpcFrameDecoder decoder, byte[] frame)
    {
        var header = frame.AsSpan().ReadLittleEndian();
        var (array, offset, end) = decoder.Decode(header, frame, Int32Size, frame.Length, MaxFrameSize);
        return array.AsSpan(offset, end - offset).ToArray();
    }

    private static byte[] NewPayload(int seed, int length)
    {
        var text = $"{{\"m\":\"ITestService.Compute\",\"a\":[{seed},\"abcdefghij\"],\"id\":";
        var result = new byte[length];
        for (var i = 0; i < length; i++)
            result[i] = (byte)text[i % text.Length];
        return result;
    }
}
