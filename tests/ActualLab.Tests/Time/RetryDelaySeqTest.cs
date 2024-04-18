namespace ActualLab.Tests.Time;

public class RetryDelaySeqTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var s = new RetryDelaySeq();
        s.Min.Should().Be(RetryDelaySeq.DefaultMin);
        s.Max.Should().Be(RetryDelaySeq.DefaultMax);
        s.Spread.Should().Be(RetryDelaySeq.DefaultSpread);
        s.Multiplier.Should().Be(RetryDelaySeq.DefaultMultiplier);

        s[-1].TotalSeconds.Should().Be(0);
        s[0].TotalSeconds.Should().Be(0);
        s[1].TotalSeconds.Should().BeApproximately(s.Min.TotalSeconds, s.Min.TotalSeconds * s.Spread * 2);
        s[100].TotalSeconds.Should().BeApproximately(s.Max.TotalSeconds, s.Max.TotalSeconds * s.Spread * 2);
    }

    [Theory]
    [InlineData(10, 100, 0.1, 2)]
    [InlineData(2, 10, 0, 1.1)]
    [InlineData(0, 5, 0.2, 1.5)]
    public void ExpShouldStartFromMin(double min, double max, double spread, double multiplier)
    {
        // arrange
        var seq = RetryDelaySeq.Exp(min, max, spread, multiplier);

        // act, assert
        seq[0].Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(min - spread));
    }
}
