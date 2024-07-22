using ActualLab.Diagnostics;

namespace ActualLab.Tests.Diagnostics;

public class ActivitySourceTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void GetActivitySourceTest()
    {
        var a = typeof(Result).GetActivitySource();
        Out.WriteLine(a.Name);
        Out.WriteLine(a.Version);
        a.Name.Should().Be("ActualLab.Core");
        a.Version.Should().StartWith("9.");
        a.Version.Should().Contain("+");

        var b = typeof(Result).GetActivitySource();
        b.Should().BeSameAs(a);
    }
}
