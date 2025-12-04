namespace ActualLab.Tests.Mathematics;

public class GuidExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var rnd = new Random();
        var buffer = new byte[16];
        for (var iteration = 0; iteration < 100; iteration++) {
            rnd.NextBytes(buffer);
            var g0 = new Guid(buffer);
            var encoded = g0.ToBase64Url();
            WriteLine(encoded);
            var g1 = GuidExt.FromBase64Url(encoded);
            g1.Should().Be(g0);
        }
    }
}
