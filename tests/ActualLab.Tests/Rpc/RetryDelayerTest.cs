namespace ActualLab.Tests.Rpc;

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
    [InlineData(0, 60)]
    [InlineData(10, 60)]
    public void RetryDelaySequenceCanBeUpTo1MLong(int minDelay, int maxDelay)
    {
        var seq = RetryDelaySeq.Exp(minDelay, maxDelay, 0);
        var minDelaySpan = TimeSpan.FromSeconds(minDelay);
        var maxDelaySpan = TimeSpan.FromSeconds(maxDelay);
        foreach (var attempt in Enumerable.Range(1, 1_000_000)) {
            var delay = seq[attempt];
            Assert.True(delay >= minDelaySpan, $"Delay can't be less than specified min = {minDelay} sec.");
            Assert.True(delay <= maxDelaySpan, $"Delay can't be greater than specified max = {maxDelay} sec.");
        }
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(10, 60)]
    public void RetryDelaySequenceIntervalsMustGrowBetweenAttempts(int minDelay, int maxDelay)
    {
        var seq = RetryDelaySeq.Exp(minDelay, maxDelay, 0);
        var maxDelaySpan = TimeSpan.FromSeconds(maxDelay);
        const int maxAttempt = 10_000;
        var prevDelay = TimeSpan.Zero;
        foreach (var attempt in Enumerable.Range(1, maxAttempt)) {
            var delay = seq[attempt];
            if (delay >= maxDelaySpan)
                break;
            if (delay <= prevDelay)
                Assert.Fail("Delays must grow between attempts.");
            prevDelay = delay;
        }
    }
}
