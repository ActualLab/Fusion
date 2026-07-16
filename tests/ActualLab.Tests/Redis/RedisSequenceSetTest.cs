using ActualLab.Redis;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Redis;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RedisSequenceSetTest(ITestOutputHelper @out) : RedisTestBase(@out)
{
    [SkipOnGitHubFact]
    public async Task ConcurrentResetTest()
    {
        var set = GetRedisDb().GetSequenceSet("concurrent-reset", 1_000);
        await set.Clear();
        await set.Reset("a", 1_000_000);

        var values = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => set.Next("a", 100)));

        values.Should().OnlyHaveUniqueItems();
        values.Should().BeEquivalentTo(Enumerable.Range(101, 100).Select(x => (long)x));
    }

    [SkipOnGitHubFact]
    public async Task BasicTest()
    {
        var set = GetRedisDb().GetSequenceSet("seq", 250);
        await set.Clear();

        (await set.Next("a")).Should().Be(1);
        (await set.Next("a")).Should().Be(2);
        (await set.Next("a", 500)).Should().Be(501);
        (await set.Next("a", 300)).Should().Be(502);
        (await set.Next("a", 200)).Should().Be(201);
        (await set.Next("a")).Should().Be(202);

        (await set.Next("a", 1000_000_000).WaitAsync(TimeSpan.FromMilliseconds(100)))
            .Should().Be(1000_000_001L); // Auto-reset test

        await set.Reset("a", 10);
        (await set.Next("a", 5)).Should().Be(11);

        const long largeValue = 9_007_199_254_740_992;
        await set.Reset("large", largeValue);
        (await set.Next("large", largeValue)).Should().Be(largeValue + 1);
    }
}
