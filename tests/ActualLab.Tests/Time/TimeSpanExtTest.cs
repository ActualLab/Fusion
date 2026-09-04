namespace ActualLab.Tests.Time;

public class TimeSpanExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ToShortStringTest()
    {
        Check(TimeSpan.Zero, "0s");
        Check(TimeSpanExt.Infinite, "inf");
        Check(TimeSpan.MinValue, "-inf");
        Check(S(0.1), "100ms");
        Check(S(-0.1), "-100ms");
#if !NETFRAMEWORK
        Check(S(0.100001), "100.001ms");
        Check(S(0.1000011), "100.001ms");
        Check(S(0.1000016), "100.002ms");
        Check(S(-0.1000016), "-100.002ms");
#endif
        Check(M(1) + S(0.11), "1m 0.11s");
        Check(M(1) + S(0.1111), "1m 0.111s");
        Check(M(-1) + S(-0.1111), "-(1m 0.111s)");
        Check(M(1) + S(1), "1m 1s");
        Check(M(-1) + S(-1), "-(1m 1s)");
        Check(H(1) + M(1) + S(1), "1h 1m 1s");
        Check(H(25) + M(1) + S(0.1), "25h 1m 0.1s");
        Check(H(25) + M(1) + S(0.11), "25h 1m 0.1s");
        Check(H(-25) + M(-1) + S(-0.11), "-(25h 1m 0.1s)");

        TimeSpan H(double value) => TimeSpan.FromHours(value);
        TimeSpan M(double value) => TimeSpan.FromMinutes(value);
        TimeSpan S(double value) => TimeSpan.FromSeconds(value);

        void Check(TimeSpan value, string expected)
            => value.ToShortString().Should().Be(expected);
    }

    [Fact]
    public void AsTimeoutTest()
    {
        TimeSpan.Zero.AsTimeout().Should().Be(TimeSpan.Zero);
        TimeSpan.FromSeconds(1).AsTimeout().Should().Be(TimeSpan.FromSeconds(1));
        TimeSpanExt.Infinite.AsTimeout().Should().Be(TimeSpanExt.Infinite);
        ((TimeSpan?)null).AsTimeout().Should().Be(TimeSpanExt.Infinite);
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSpan.FromSeconds(-1).AsTimeout());

        0d.AsTimeout().Should().Be(TimeSpan.Zero);
        1.5.AsTimeout().Should().Be(TimeSpan.FromSeconds(1.5));
        double.PositiveInfinity.AsTimeout().Should().Be(TimeSpanExt.Infinite);
        TimeSpanExt.InfiniteInSeconds.AsTimeout().Should().Be(TimeSpanExt.Infinite);
        ((double?)null).AsTimeout().Should().Be(TimeSpanExt.Infinite);
        Assert.Throws<ArgumentOutOfRangeException>(() => (-1d).AsTimeout());
        Assert.Throws<ArgumentOutOfRangeException>(() => double.NaN.AsTimeout());
    }
}
