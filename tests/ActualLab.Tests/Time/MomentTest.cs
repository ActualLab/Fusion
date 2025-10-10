using System.Globalization;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class MomentTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var m = Moment.Now;
        var m1 = (Moment)m.ToDateTimeOffset();
        m1.Should().Be(m);
        m1 = m.ToDateTime();
        m1.Should().Be(m);

        m1 = m.PassThroughAllSerializers(Out);
        m1.Should().Be(m);
    }

    [Fact]
    public void UtcHandlingTest()
    {
        var epsilon = TimeSpan.FromSeconds(1);

        var now1 = (Moment) DateTime.UtcNow;
        var now2 = (Moment) DateTime.Now;
        Math.Abs((now1 - now2).Ticks).Should().BeLessThan(epsilon.Ticks);

        now1 = DateTimeOffset.UtcNow;
        now2 = DateTimeOffset.Now;
        Math.Abs((now1 - now2).Ticks).Should().BeLessThan(epsilon.Ticks);
    }

    [Fact]
    public void ClampTest()
    {
        var m = new Moment(DateTime.Now);
        m.Clamp(m, m).Should().Be(m);
        m.Clamp(m + TimeSpan.FromSeconds(1), DateTime.MaxValue)
            .Should().Be(m + TimeSpan.FromSeconds(1));
        m.Clamp(DateTime.MinValue, m - TimeSpan.FromSeconds(1))
            .Should().Be(m - TimeSpan.FromSeconds(1));

        m = Moment.MinValue;
        m.ToDateTimeClamped().Should().Be(DateTime.MinValue.ToUniversalTime());
        m.ToDateTimeOffsetClamped().Should().Be(DateTimeOffset.MinValue.ToUniversalTime());
        m.ToString().Should().Be(new Moment(DateTime.MinValue).ToString());
        m = Moment.MaxValue;
        m.ToDateTimeClamped().Should().Be(DateTime.MaxValue.ToUniversalTime());
        m.ToDateTimeOffsetClamped().Should().Be(DateTimeOffset.MaxValue.ToUniversalTime());
        m.ToString().Should().Be(new Moment(DateTimeOffset.MaxValue).ToString());
    }

    [Fact]
    public void FloorTest()
    {
        // After Unix epoch
        TestFloor("2024-03-15T14:32:47.0000000Z", TimeSpan.FromMilliseconds(123), TimeSpan.FromSeconds(1));
        TestFloor("2024-03-15T14:32:00.0000000Z", TimeSpan.FromSeconds(47.123), TimeSpan.FromMinutes(1));
        TestFloor("2024-03-15T14:00:00.0000000Z", TimeSpan.FromMinutes(32) + TimeSpan.FromSeconds(47.123), TimeSpan.FromHours(1));
        TestFloor("2024-03-15T00:00:00.0000000Z", TimeSpan.FromHours(14) + TimeSpan.FromMinutes(32) + TimeSpan.FromSeconds(47.123), TimeSpan.FromDays(1));
        TestFloor("2024-03-15T14:30:00.0000000Z", TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(47.123), TimeSpan.FromMinutes(15));

        // Before Unix epoch (negative epoch offset)
        TestFloor("1965-06-20T08:15:32.0000000Z", TimeSpan.FromMilliseconds(567), TimeSpan.FromSeconds(1));
        TestFloor("1965-06-20T08:15:00.0000000Z", TimeSpan.FromSeconds(32.567), TimeSpan.FromMinutes(1));
        TestFloor("1965-06-20T08:00:00.0000000Z", TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(32.567), TimeSpan.FromHours(1));
        TestFloor("1965-06-20T00:00:00.0000000Z", TimeSpan.FromHours(8) + TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(32.567), TimeSpan.FromDays(1));
        TestFloor("1965-06-20T08:15:00.0000000Z", TimeSpan.FromSeconds(32.567), TimeSpan.FromMinutes(15));

        // Edge case: exact boundary (offset = 0)
        TestFloor("2024-03-15T14:32:00.0000000Z", TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void CeilingTest()
    {
        // After Unix epoch
        TestCeiling("2024-03-15T14:32:48.0000000Z", TimeSpan.FromMilliseconds(-877), TimeSpan.FromSeconds(1));
        TestCeiling("2024-03-15T14:33:00.0000000Z", TimeSpan.FromSeconds(-12.877), TimeSpan.FromMinutes(1));
        TestCeiling("2024-03-15T15:00:00.0000000Z", TimeSpan.FromMinutes(-27) + TimeSpan.FromSeconds(-12.877), TimeSpan.FromHours(1));
        TestCeiling("2024-03-16T00:00:00.0000000Z", TimeSpan.FromHours(-9) + TimeSpan.FromMinutes(-27) + TimeSpan.FromSeconds(-12.877), TimeSpan.FromDays(1));
        TestCeiling("2024-03-15T14:45:00.0000000Z", TimeSpan.FromMinutes(-12) + TimeSpan.FromSeconds(-12.877), TimeSpan.FromMinutes(15));

        // Before Unix epoch (negative epoch offset)
        TestCeiling("1965-06-20T08:15:33.0000000Z", TimeSpan.FromMilliseconds(-433), TimeSpan.FromSeconds(1));
        TestCeiling("1965-06-20T08:16:00.0000000Z", TimeSpan.FromSeconds(-27.433), TimeSpan.FromMinutes(1));
        TestCeiling("1965-06-20T09:00:00.0000000Z", TimeSpan.FromMinutes(-44) + TimeSpan.FromSeconds(-27.433), TimeSpan.FromHours(1));
        TestCeiling("1965-06-21T00:00:00.0000000Z", TimeSpan.FromHours(-15) + TimeSpan.FromMinutes(-44) + TimeSpan.FromSeconds(-27.433), TimeSpan.FromDays(1));
        TestCeiling("1965-06-20T08:30:00.0000000Z", TimeSpan.FromMinutes(-14) + TimeSpan.FromSeconds(-27.433), TimeSpan.FromMinutes(15));

        // Edge case: exact boundary (offset = 0)
        TestCeiling("2024-03-15T14:32:00.0000000Z", TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RoundTest()
    {
        // After Unix epoch - rounds down (offset < half interval)
        TestRound("2024-03-15T14:32:47.0000000Z", TimeSpan.FromMilliseconds(123), TimeSpan.FromSeconds(1));
        TestRound("2024-03-15T14:32:00.0000000Z", TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(1));
        TestRound("2024-03-15T14:00:00.0000000Z", TimeSpan.FromMinutes(25), TimeSpan.FromHours(1));
        TestRound("2024-03-15T00:00:00.0000000Z", TimeSpan.FromHours(10), TimeSpan.FromDays(1));

        // After Unix epoch - rounds up (offset >= half interval)
        TestRound("2024-03-15T14:32:48.0000000Z", TimeSpan.FromMilliseconds(-500), TimeSpan.FromSeconds(1));
        TestRound("2024-03-15T14:33:00.0000000Z", TimeSpan.FromSeconds(-30), TimeSpan.FromMinutes(1));
        TestRound("2024-03-15T15:00:00.0000000Z", TimeSpan.FromMinutes(-30), TimeSpan.FromHours(1));
        TestRound("2024-03-16T00:00:00.0000000Z", TimeSpan.FromHours(-12), TimeSpan.FromDays(1));

        // Before Unix epoch (negative epoch offset) - rounds down
        TestRound("1965-06-20T08:15:32.0000000Z", TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1));
        TestRound("1965-06-20T08:15:00.0000000Z", TimeSpan.FromSeconds(25), TimeSpan.FromMinutes(1));
        TestRound("1965-06-20T08:00:00.0000000Z", TimeSpan.FromMinutes(20), TimeSpan.FromHours(1));
        TestRound("1965-06-20T00:00:00.0000000Z", TimeSpan.FromHours(8), TimeSpan.FromDays(1));

        // Before Unix epoch (negative epoch offset) - rounds up
        TestRound("1965-06-20T08:15:33.0000000Z", TimeSpan.FromMilliseconds(-500), TimeSpan.FromSeconds(1));
        TestRound("1965-06-20T08:16:00.0000000Z", TimeSpan.FromSeconds(-30), TimeSpan.FromMinutes(1));
        TestRound("1965-06-20T09:00:00.0000000Z", TimeSpan.FromMinutes(-30), TimeSpan.FromHours(1));
        TestRound("1965-06-21T00:00:00.0000000Z", TimeSpan.FromHours(-12), TimeSpan.FromDays(1));

        // Edge cases: exact boundary and half-way point
        TestRound("2024-03-15T14:32:00.0000000Z", TimeSpan.Zero, TimeSpan.FromMinutes(1));
        TestRound("2024-03-15T14:33:00.0000000Z", TimeSpan.FromSeconds(-30), TimeSpan.FromMinutes(1)); // Exactly halfway rounds up
    }

    // Private methods

    private void TestFloor(string expectedDateTime, TimeSpan offset, TimeSpan unit)
    {
        var parsed = DateTime.ParseExact(expectedDateTime, "O", CultureInfo.InvariantCulture).ToUniversalTime();
        var expected = new Moment(parsed);
        var input = expected + offset;
        input.Floor(unit).Should().Be(expected);
    }

    private void TestCeiling(string expectedDateTime, TimeSpan offset, TimeSpan unit)
    {
        var parsed = DateTime.ParseExact(expectedDateTime, "O", CultureInfo.InvariantCulture).ToUniversalTime();
        var expected = new Moment(parsed);
        var input = expected + offset;
        input.Ceiling(unit).Should().Be(expected);
    }

    private void TestRound(string expectedDateTime, TimeSpan offset, TimeSpan unit)
    {
        var parsed = DateTime.ParseExact(expectedDateTime, "O", CultureInfo.InvariantCulture).ToUniversalTime();
        var expected = new Moment(parsed);
        var input = expected + offset;
        input.Round(unit).Should().Be(expected);
    }
}
