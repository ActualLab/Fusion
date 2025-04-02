namespace ActualLab.Tests.Mathematics;

public class RangeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void LongRangeTest()
    {
        var r1 = new Range<long>(100, 120);
        r1.IsEmpty.Should().BeFalse();
        var r2 = new Range<long>(140, 150);
        r2.IsEmpty.Should().BeFalse();
        var r3 = r1.IntersectWith(r2);
        Out.WriteLine(r3.ToString()); // {"start":140,"end":140}
        r3.IsEmpty.Should().BeTrue();

        r1 = new Range<long>(100, 200);
        r2 = new Range<long>(120, 180);
        r1.IntersectWith(r2).Should().Be(new Range<long>(120, 180));
    }
}
