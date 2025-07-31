using ActualLab.OS;

namespace ActualLab.Tests.OS;

public class MiscInfoTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void OSInfoTest()
    {
        WriteLine($"OS type:   {OSInfo.Kind}");
        WriteLine($"User home: {OSInfo.UserHomePath}");
    }

    [Fact]
    public void HardwareInfoTest()
    {
        var processorCount = HardwareInfo.GetProcessorCountFactor();
        processorCount.Should().BeGreaterThan(0);
        WriteLine($"CPU core count: {processorCount}");
    }

#if !NETFRAMEWORK

    [Fact]
    public void DotNetCoreInfoTest()
    {
        var version = RuntimeInfo.DotNet.Version;
        var versionString = RuntimeInfo.DotNet.VersionString;
        if (version is null) {
#if NET7_0
        versionString.Should().StartWith("7.");
#elif NET6_0
        versionString.Should().StartWith("6.");
#elif NET5_0
        versionString.Should().StartWith("5.");
#endif
        }
        else
            version.Should().BeGreaterThan(Version.Parse("3.0"));
        WriteLine($".NET Core version: {version}");
    }

#endif

    private void WriteLine(string line) => Out.WriteLine(line);
}
