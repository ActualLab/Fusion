namespace ActualLab.Tests.Time;

public class RetryDelaySeqTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void FixedTest()
    {
        var s = RetryDelaySeq.Fixed(1).AssertPassesThroughAllSerializers();
        s.Min.Should().Be(TimeSpan.FromSeconds(1));
        s.Max.Should().Be(TimeSpan.FromSeconds(1));
        s.Spread.Should().Be(RetryDelaySeq.DefaultSpread);
        s.Multiplier.Should().Be(1);

        s[-1].TotalSeconds.Should().Be(0);
        s[0].TotalSeconds.Should().Be(0);
        s[1].TotalSeconds.Should().BeApproximately(s.Min.TotalSeconds, s.Min.TotalSeconds * s.Spread * 2);
        s[100].TotalSeconds.Should().BeApproximately(s.Max.TotalSeconds, s.Max.TotalSeconds * s.Spread * 2);

        s = s with { Spread = 0d };
        s.Delays(0, 0).Should().BeEmpty();
        s.Delays(0, 1).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 0d });
        s.Delays(0, 2).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 0d, 1d });
        s.Delays(1, 1).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 1d });
    }

    [Fact]
    public void ExpTest()
    {
        var s = RetryDelaySeq.Exp(1, 10).AssertPassesThroughAllSerializers();
        s.Min.Should().Be(TimeSpan.FromSeconds(1));
        s.Max.Should().Be(TimeSpan.FromSeconds(10));
        s.Spread.Should().Be(RetryDelaySeq.DefaultSpread);
        s.Multiplier.Should().Be(RetryDelaySeq.DefaultMultiplier);

        s[-1].TotalSeconds.Should().Be(0);
        s[0].TotalSeconds.Should().Be(0);
        s[1].TotalSeconds.Should().BeApproximately(s.Min.TotalSeconds, s.Min.TotalSeconds * s.Spread * 2);
        s[100].TotalSeconds.Should().BeApproximately(s.Max.TotalSeconds, s.Max.TotalSeconds * s.Spread * 2);

        s = s with { Spread = 0d, Multiplier = 2d };
        s.Delays(0, 0).Should().BeEmpty();
        s.Delays(0, 1).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 0d });
        s.Delays(0, 2).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 0d, 1d });
        s.Delays(0, 3).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 0d, 1d, 2d });
        s.Delays(1, 1).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 1d });
        s.Delays(1, 2).Select(x => x.TotalSeconds).Should().BeEquivalentTo(new[] { 1d, 2d });
    }

    [Fact]
    public void WrongMinDelayTest()
    {
        var s = RetryDelaySeq.Exp(0, 1);
        Assert.Throws<InvalidOperationException>(() => s[0]);
        Assert.Throws<InvalidOperationException>(() => s[1]);
    }

    [Theory]
    [InlineData(0.1, 60)]
    [InlineData(10, 60)]
    public void RetryDelaySequenceCanBeUpTo1MLong(double minDelay, double maxDelay)
    {
        var seq = RetryDelaySeq.Exp(minDelay, maxDelay, 0);
        var minDelaySpan = TimeSpan.FromSeconds(minDelay) * (0.99 - seq.Spread);
        var maxDelaySpan = TimeSpan.FromSeconds(maxDelay) * (1.01 + seq.Spread);
        foreach (var delay in seq.Delays(1, 1_000_000)) {
            Assert.True(delay >= minDelaySpan, $"Delay can't be less than specified min = {minDelay} sec.");
            Assert.True(delay <= maxDelaySpan, $"Delay can't be greater than specified max = {maxDelay} sec.");
        }
    }

    [Theory]
    [InlineData(0.1, 60)]
    [InlineData(10, 60)]
    public void RetryDelaySequenceIntervalsMustGrowBetweenAttempts(double minDelay, double maxDelay)
    {
        var seq = RetryDelaySeq.Exp(minDelay, maxDelay, 0);
        var maxDelaySpan = TimeSpan.FromSeconds(maxDelay);
        var prevDelay = TimeSpan.Zero;
        foreach (var delay in seq.Delays(1, 10_000)) {
            if (delay >= maxDelaySpan)
                break;
            if (delay <= prevDelay)
                Assert.Fail("Delays must grow between attempts.");
            prevDelay = delay;
        }
    }
}
