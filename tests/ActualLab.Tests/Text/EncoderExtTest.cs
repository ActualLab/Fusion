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
}
