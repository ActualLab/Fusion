using ActualLab.Generators;

namespace ActualLab.Tests.Text;

public class ByteStringTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void Utf8EncodeDecodeTest()
    {
        var rnd = new RandomStringGenerator();
        for (var length = 0; length < 2100; length++) {
            for (var iteration = 0; iteration < 50; iteration++) {
                var s = rnd.Next(length);
                var bytes = ByteString.FromStringAsUtf8(s);
                var decoded = bytes.ToStringAsUtf8();
                decoded.Should().Be(decoded);
            }
        }
    }

    [Fact]
    public void Utf16EncodeDecodeTest()
    {
        var rnd = new RandomStringGenerator();
        for (var length = 0; length < 2100; length++) {
            for (var iteration = 0; iteration < 50; iteration++) {
                var s = rnd.Next(length);
                var bytes = ByteString.FromStringAsUtf16(s);
                var decoded = bytes.ToStringAsUtf16();
                decoded.Should().Be(decoded);
            }
        }
    }

    [Fact]
    public void Base64UrlEncodeDecodeTest()
    {
        var rnd = new Random();
        for (var length = 0; length < 2100; length++) {
            var bytes = new byte[length];
            for (var iteration = 0; iteration < 50; iteration++) {
                rnd.NextBytes(bytes);
                var s = new ByteString(bytes);
                var encoded = s.ToBase64Url(); // Let's also test this
                if (iteration < 1 && length < 32)
                    Out.WriteLine($"{length}: {encoded}");

                var decoded = ByteString.FromBase64Url(encoded);
                decoded.Should().Be(bytes);
                decoded.GetHashCode().Should().Be(bytes.GetHashCode());
            }
        }
    }

    [Fact]
    public void Base64EncodeDecodeTest()
    {
        var rnd = new Random();
        for (var length = 0; length < 2100; length++) {
            var bytes = new byte[length];
            for (var iteration = 0; iteration < 50; iteration++) {
                rnd.NextBytes(bytes);
                var s = new ByteString(bytes);
                var encoded = s.ToBase64(); // Let's also test this
                if (iteration < 1 && length < 32)
                    Out.WriteLine($"{length}: {encoded}");

                var decoded = ByteString.FromBase64(encoded);
                decoded.Should().Be(bytes);
                decoded.GetHashCode().Should().Be(bytes.GetHashCode());
            }
        }
    }
}
