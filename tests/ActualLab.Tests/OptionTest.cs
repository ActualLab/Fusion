namespace ActualLab.Tests;

public class OptionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var o1 = Option.None<int>();
        o1.ValueOrDefault.Should().Be(0);

        var o2 = Option.None<int?>();
        o2.ValueOrDefault.Should().Be(null);

        var o3 = Option.None<string>();
        o2.ValueOrDefault.Should().Be(null);
    }
}
