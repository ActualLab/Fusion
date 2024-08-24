namespace ActualLab.Tests.Time;

public class TimeSpanExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ToShortStringTest()
    {
        Check(S(0.1), "100ms");
        Check(S(0.100001), "100.001ms");
        Check(S(0.1000011), "100.001ms");
        Check(S(0.1000016), "100.002ms");
        Check(M(1) + S(0.11), "1m 0.11s");
        Check(M(1) + S(0.1111), "1m 0.111s");
        Check(M(1) + S(1), "1m 1s");
        Check(H(1) + M(1) + S(1), "1h 1m 1s");
        Check(H(25) + M(1) + S(0.1), "25h 1m 0.1s");
        Check(H(25) + M(1) + S(0.11), "25h 1m 0.1s");

        TimeSpan H(double value) => TimeSpan.FromHours(value);
        TimeSpan M(double value) => TimeSpan.FromMinutes(value);
        TimeSpan S(double value) => TimeSpan.FromSeconds(value);

        void Check(TimeSpan value, string expected)
            => value.ToShortString().Should().Be(expected);
    }
}
