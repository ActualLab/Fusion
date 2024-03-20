using System.Collections.Concurrent;
using ActualLab.OS;

namespace ActualLab.Fusion.Tests;

public class ComputedVersionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var maxDegreeOfParallelism = HardwareInfo.ProcessorCountPo2 * 10;
        var versions = new ConcurrentBag<ulong>();
        var versionCount = maxDegreeOfParallelism * 1000;
        var result = Parallel.For(
            0, versionCount,
            new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            _ => versions.Add(ComputedVersion.Next()));

        result.IsCompleted.Should().BeTrue();
        versions.Distinct().Count().Should().Be(versionCount);
        versions.All(x => x != 0ul).Should().BeTrue();
    }
}
