using ActualLab.Reflection;
using ActualLab.Testing.Logging;

namespace ActualLab.Tests.Testing;

public class CapturingLoggerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var clp = new CapturingLoggerProvider();
        var c = new ServiceCollection()
            .AddLogging(l => l.ClearProviders()
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(clp))
            .BuildServiceProvider();
        var log = c.LogFor(GetType());
        log.LogDebug("Debug");
        log.LogInformation("Info");
        log.LogWarning("Warning");

        var content = clp.Content;
        WriteLine(content);
        content.Should().NotContain("Debug");
        content.Should().Contain("I ");
        content.Should().Contain("Info");
        content.Should().Contain("W ");
        content.Should().Contain("Warning");
        content.Should().Contain(GetType().GetName());
    }
}
