using ActualLab.Diagnostics;

namespace ActualLab.Tests.Diagnostics;

public class AssemblyExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void IsDebugBuildTest()
    {
#if DEBUG
        const bool isDebug = true;
#else
        const bool isDebug = false;
#endif
        GetType().Assembly.IsDebugBuild().Should().Be(isDebug);
        typeof(object).Assembly.IsDebugBuild().Should().BeFalse();
    }
}
