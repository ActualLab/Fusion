using CommunityToolkit.HighPerformance;

namespace ActualLab.Tests.Text;

#pragma warning disable CA1820

public class StringTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void EqualsTest()
    {
        string.Equals(null, null, StringComparison.Ordinal).Should().BeTrue();
        string.Equals(null, "", StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void StableHashTest()
    {
        "abc".GetDjb2HashCode().Should().Be(-1549454715);
        "abc".GetXxHash3().Should().Be(-1701228566);
    }
}
