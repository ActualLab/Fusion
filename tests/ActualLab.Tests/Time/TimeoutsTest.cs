namespace ActualLab.Tests.Time;

public class TimeoutsTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void KeepAliveSlotTest()
    {
        var q = Timeouts.Quanta;
        q.TotalSeconds.Should().BeGreaterThan(0.2);
        q.TotalSeconds.Should().BeLessThan(0.21);

        Timeouts.GetKeepAliveSlot(Timeouts.StartedAt).Should().Be(0);
        Timeouts.GetKeepAliveSlot(Timeouts.StartedAt + q).Should().Be(1);
        Timeouts.GetKeepAliveSlot(Timeouts.StartedAt + q.MultiplyBy(2)).Should().Be(2);
    }
}
