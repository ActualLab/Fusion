namespace ActualLab.Tests.Text;

public class Base64UrlEncoderTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var rnd = new Random();
        for (var length = 0; length < 2100; length++) {
            var source = new byte[length];
            for (var iteration = 0; iteration < 50; iteration++) {
                rnd.NextBytes(source);
                var encoded = Base64UrlEncoder.Encode(source);
                if (iteration < 1 && length < 32)
                    Out.WriteLine($"{length}: {encoded}");

                var decoded = Base64UrlEncoder.Decode(encoded).ToArray();
                decoded.SequenceEqual(source).Should().BeTrue();
            }
        }
    }
}
