using ActualLab.Versioning;

namespace ActualLab.Tests.Versioning;

public class CpuTimestampBasedVersionGeneratorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void NextVersion_ShouldReturnPositiveValue()
    {
        var g = CpuTimestampBasedVersionGenerator.Instance;
        var v = g.NextVersion();
        v.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NextVersion_ShouldBeMonotonicallyIncreasing()
    {
        var g = CpuTimestampBasedVersionGenerator.Instance;
        var v1 = g.NextVersion();
        var v2 = g.NextVersion(v1);
        var v3 = g.NextVersion(v2);
        v2.Should().BeGreaterThan(v1);
        v3.Should().BeGreaterThan(v2);
    }

    [Fact]
    public void NextVersion_WithLargeCurrentVersion_ShouldIncrementByOne()
    {
        var g = CpuTimestampBasedVersionGenerator.Instance;
        var largeVersion = long.MaxValue - 1;
        var next = g.NextVersion(largeVersion);
        next.Should().Be(long.MaxValue);
    }

    [Fact]
    public void NextVersion_WithZero_ShouldReturnTimestampBased()
    {
        var g = CpuTimestampBasedVersionGenerator.Instance;
        var v = g.NextVersion();
        v.Should().BeGreaterThan(0);
    }
}
