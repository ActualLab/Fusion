using ActualLab.Diagnostics;

namespace ActualLab.Tests.Diagnostics;

public class MeterTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void GetMeterTest()
    {
        var a = typeof(Result).GetMeter();
        Out.WriteLine(a.Name);
        Out.WriteLine(a.Version);
        a.Name.Should().Be("ActualLab.Core");
        a.Version.Should().StartWith("7.");
        a.Version.Should().Contain("+");

        var b = typeof(Result).GetMeter();
        b.Should().BeSameAs(a);
    }
}
