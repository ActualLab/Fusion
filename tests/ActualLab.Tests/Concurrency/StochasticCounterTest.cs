using ActualLab.Concurrency;
using ActualLab.OS;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Concurrency;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class StochasticCounterTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var c = new StochasticCounter();
        c.Precision.Should().Be(0);
        c.Value.Should().Be(0);

        c.TryIncrement(1).Should().BeTrue();
        c.Value.Should().Be(0);
        c.TryIncrement(1).Should().BeTrue();
        c.Value.Should().Be(0);

        c.TryDecrement(0).Should().BeTrue();
        c.Value.Should().Be(0);
        c.TryDecrement(0).Should().BeTrue();
        c.Value.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentTest()
    {
        var c = new StochasticCounter(StochasticCounter.DefaultPrecision);
        c.Precision.Should().Be(HardwareInfo.ProcessorCountPo2);
        c.Value.Should().Be(0);

        const int iterationCount = 1_000_000;
        const int runCount = 5;
        var agentCount = HardwareInfo.GetProcessorCountFraction(2);
        var incrementCount = (double)(agentCount * iterationCount);
        Out.WriteLine($"Increment count: {incrementCount}");
        for (var i = 0; i < runCount; i++) {
            c.Reset();
            var startedAt = CpuTimestamp.Now;
            var tasks = Enumerable.Range(0, agentCount)
                .Select(_ => Task.Run(() => {
                    var min = 0;
                    var max = 0;
                    var actualIncrementCount = 0;
                    var actualDecrementCount = 0;
                    for (var j = 0; j < iterationCount; j++) {
                        if (c.Increment() is { } max1) {
                            actualIncrementCount++;
                            max = Math.Max(max, max1);
                        }

                        if (c.Decrement() is { } min1) {
                            actualDecrementCount++;
                            min = Math.Min(min, min1);
                        }
                        if (j % 250_000 == 0)
                            Out.WriteLine($"- {j}: ({min}, {max}), counts: ({actualIncrementCount}, {actualDecrementCount})");
                    }
                    return (min, max);
                }))
                .ToArray();
            var results = await Task.WhenAll(tasks);
            var duration = (CpuTimestamp.Now - startedAt).TotalSeconds;
            var ops = (incrementCount * 2) / duration;
            var min = results.Min(x => x.min);
            var max = results.Min(x => x.max);
            var value = c.Value;
            var deviation = Math.Max(Math.Max(max, -min), Math.Abs(value));
            var pDeviation = deviation / incrementCount;

            Out.WriteLine($"{i}: {pDeviation:P2} ({deviation}) -> {ops/1000_000:N1}M ops/s");
            pDeviation.Should().BeInRange(-0.01, 0.05);
        }
    }
}
