namespace ActualLab.Tests.Collections;

public class PruningCacheTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PruningCache<string, int>(1));

        var cache = new PruningCache<string, int>(8);
        cache.Capacity.Should().Be(8);
        cache.Count.Should().Be(0);
        cache.TryGet("a", out _).Should().BeFalse();

        cache.TryAdd("a", 1).Should().BeTrue();
        cache.TryAdd("a", 2).Should().BeFalse();
        cache.TryGet("a", out var value).Should().BeTrue();
        value.Should().Be(1);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task PruneTest()
    {
        const int capacity = 8;
        var cache = new PruningCache<int, int>(capacity);
        for (var i = 0; i < 100; i++)
            cache.TryAdd(i, i);

        await cache.Prune();
        cache.Count.Should().BeInRange(1, capacity);

        // The entries that survived must still be the ones that were added
        for (var i = 0; i < 100; i++)
            if (cache.TryGet(i, out var value))
                value.Should().Be(i);
    }

    [Fact]
    public async Task ConcurrentTest()
    {
        const int threadCount = 8;
        const int capacity = 32;
        const int hotKeyCount = 8;
        const int coldKeyCount = 1000;

        var cache = new PruningCache<int, string>(capacity);
        var tasks = Enumerable.Range(0, threadCount)
            .Select(threadIndex => Task.Run(() => {
                for (var i = 0; i < coldKeyCount; i++) {
                    Access(i % hotKeyCount);
                    Access(hotKeyCount + (((i * 7) + threadIndex) % coldKeyCount));
                }
                return;

                void Access(int key) {
                    if (cache.TryGet(key, out var value))
                        value.Should().Be(FormatValue(key));
                    else
                        cache.TryAdd(key, FormatValue(key));
                }
            }))
            .ToArray();
        await Task.WhenAll(tasks);

        await cache.Prune();
        cache.Count.Should().BeLessThanOrEqualTo(capacity);
    }

    // Private methods

    private static string FormatValue(int key)
        => $"value-{key}";
}
