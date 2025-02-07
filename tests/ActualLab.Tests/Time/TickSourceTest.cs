using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TickSourceTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var delays = new[] { 1, 10, 15, 16, 30 }.Select(x => TimeSpan.FromMilliseconds(x)).ToArray();
        var maxShrinkage = TimeSpan.FromMilliseconds(3);
        foreach (var delay in delays) {
            Out.WriteLine("");
            Out.WriteLine($"Delay: {delay.ToShortString()}");
            var tickSource = new TickSource(delay);
            var now = CpuTimestamp.Now;
            var lastNow = now;
            var minElapsed = TimeSpan.FromSeconds(10);
            for (var i = 0; i < 50; i++) {
                await tickSource.WhenNextTick();
                var elapsed = lastNow.Elapsed;
                elapsed.Should().BeGreaterThanOrEqualTo(delay - maxShrinkage);
                if (i < 5)
                    Out.WriteLine($"- {elapsed.TotalMilliseconds:F3}ms");
                if (i > 1)
                    minElapsed = TimeSpanExt.Min(minElapsed, elapsed);
                lastNow = CpuTimestamp.Now;
            }
            Out.WriteLine($"Min. elapsed: {minElapsed.TotalMilliseconds:F3}ms");
        }
    }

    [Fact]
    public async Task ThreadPoolContinuationTest()
    {
        var tickSource = new TickSource(TimeSpan.FromMilliseconds(10));
        var threadIds = (await Enumerable.Range(0, 10_000)
            .Select(async _ => {
                await tickSource.WhenNextTick().ConfigureAwait(false);
                return Environment.CurrentManagedThreadId;
            })
            .Collect()
            ).Distinct()
            .ToList();
        Out.WriteLine($"ThreadIDs: {threadIds.ToDelimitedString()}");
        threadIds.Count.Should().BeGreaterThan(1);
    }
}
