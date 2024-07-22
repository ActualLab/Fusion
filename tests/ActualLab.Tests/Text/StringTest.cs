namespace ActualLab.Tests.Text;

#pragma warning disable CA1820

public class StringTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public static void EqualsTest()
    {
        string.Equals(null, null, StringComparison.Ordinal).Should().BeTrue();
        string.Equals(null, "", StringComparison.Ordinal).Should().BeFalse();
    }
}
