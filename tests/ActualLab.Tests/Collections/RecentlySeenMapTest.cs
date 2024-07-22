namespace ActualLab.Tests.Collections;

public class RecentlySeenMapTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var map = new RecentlySeenMap<string, Unit>(2, TimeSpan.FromSeconds(10));

        // TryAdd test
        map.TryAdd("a").Should().BeTrue();
        map.TryGet("a", out _).Should().BeTrue();
        map.TryGet("x", out _).Should().BeFalse();

        await Task.Delay(50);
        map.TryAdd("b").Should().BeTrue();
        map.TryGet("a", out _).Should().BeTrue();
        map.TryGet("b", out _).Should().BeTrue();
        map.TryGet("x", out _).Should().BeFalse();

        await Task.Delay(50);
        map.TryAdd("c").Should().BeTrue();
        map.TryGet("a", out _).Should().BeFalse();
        map.TryGet("b", out _).Should().BeTrue();
        map.TryGet("c", out _).Should().BeTrue();
        map.TryGet("x", out _).Should().BeFalse();

        // TryRemove test
        map.TryRemove("c").Should().BeTrue();
        map.TryGet("b", out _).Should().BeTrue();
        map.TryGet("c", out _).Should().BeFalse();

        map.TryRemove("b").Should().BeTrue();
        map.TryGet("b", out _).Should().BeFalse();

        // Prune test
        map.Prune();
    }
}
