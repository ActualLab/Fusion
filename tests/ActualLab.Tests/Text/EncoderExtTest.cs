using System.Text;
using ActualLab.IO.Internal;

namespace ActualLab.Tests.Text;

public class EncoderExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("текст")]
    public void ConvertRoundTripTest(string text)
    {
        var bytes = EncodingExt.Utf8NoBom.GetEncoder().Convert(text.AsSpan());
        bytes.Should().Equal(Encoding.UTF8.GetBytes(text));

        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, 16, mustClear: false);
        EncodingExt.Utf8NoBom.GetEncoder().Convert(text.AsSpan(), buffer);
        buffer.WrittenSpan.ToArray().Should().Equal(bytes);

        EncodingExt.Utf8NoBom.GetDecoder().Convert(bytes).Should().Be(text);
    }

    [Fact]
    public void EmptyInputFlushesEncoderTest()
    {
        var encoder = EncodingExt.Utf8NoBom.GetEncoder();
        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, 16, mustClear: false);
        encoder.Convert("\ud800".AsSpan(), buffer, flush: false); // A dangling high surrogate
        buffer.WrittenCount.Should().Be(0);

        encoder.Convert(ReadOnlySpan<char>.Empty, buffer, flush: true);
        buffer.WrittenSpan.ToArray().Should().Equal(0xEF, 0xBF, 0xBD); // U+FFFD
    }

    [Fact]
    public void EmptyInputFlushesDecoderTest()
    {
        var decoder = EncodingExt.Utf8NoBom.GetDecoder();
        using var buffer = new ArrayPoolBuffer<char>(ArrayPools.SharedCharPool, 16, mustClear: false);
        decoder.Convert(new byte[] { 0xD0 }, buffer, flush: false); // The first byte of a 2-byte sequence
        buffer.WrittenCount.Should().Be(0);

        decoder.Convert(ReadOnlySpan<byte>.Empty, buffer, flush: true);
        buffer.WrittenSpan.ToString().Should().Be("�");
    }
}
