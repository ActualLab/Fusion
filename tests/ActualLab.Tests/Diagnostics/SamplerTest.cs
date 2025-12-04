using ActualLab.Diagnostics;
using ActualLab.OS;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Diagnostics;

[Collection(nameof(PerformanceTests)), Trait("Category", nameof(PerformanceTests))]
public class SamplerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void AlwaysNeverTest()
    {
        Sampler.Always.Duplicate().Should().BeSameAs(Sampler.Always);
        Sampler.Never.Duplicate().Should().BeSameAs(Sampler.Never);
    }

    [Fact]
    public async Task TestAll()
    {
        var samplers = new [] {
            Sampler.Random(0.25).ToConcurrent(),
            Sampler.Random(0.25),
            Sampler.RandomShared(0.25),
            Sampler.AlternativeRandom(0.25),
            Sampler.AlternativeRandom(0.25).ToConcurrent(),
            Sampler.Random(0.05).ToConcurrent(),
            Sampler.Random(0.05),
            Sampler.RandomShared(0.05),
            Sampler.AlternativeRandom(0.05),
            Sampler.AlternativeRandom(0.05).ToConcurrent(),
            Sampler.Random(0.01),
            Sampler.RandomShared(0.01),
            Sampler.AlternativeRandom(0.01),
            Sampler.EveryNth(10),
            Sampler.EveryNth(8),
            Sampler.Never,
            Sampler.Always,
        };
        WriteLine("Regular tests:");
        foreach (var sampler in samplers)
            Test(sampler);

        WriteLine("");
        WriteLine("Performance tests:");
        foreach (var sampler in samplers)
            await PerformanceTest(sampler);

        await PerformanceTest(Sampler.AlternativeRandom(0.001));
    }

    private void Test(Sampler sampler)
    {
        WriteLine($"{sampler}:");

        var opCount = 1_000_000;
        var replies = new bool[opCount];
        for (var testIndex = 0; testIndex < 3; testIndex++) {
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < opCount; i++)
                replies[i] = sampler.Next();
            sw.Stop();

            var p = (double)replies.Count(x => x) / replies.Length;
            var error = p > 0
                ? p / sampler.Probability - 1
                : p - sampler.Probability;
            WriteLine($"- Error: {error:P2}");
            error.Should().BeLessThan(0.05);

            var rate = opCount / sw.Elapsed.TotalSeconds;
            if (testIndex == 2)
                WriteLine($"- {rate:N3} ops/s");
        }
    }

    private async Task PerformanceTest(Sampler sampler)
    {
        WriteLine($"{sampler}:");

        var opCount = 200_000;
        var tasks = new Task[HardwareInfo.ProcessorCount / 2];
        var sw = Stopwatch.StartNew();
        for (var taskIndex = 0; taskIndex < tasks.Length; taskIndex++)
            tasks[taskIndex] = Task.Run(Run);
        await Task.WhenAll(tasks).ConfigureAwait(false);

        sw.Stop();
        var rate = opCount / sw.Elapsed.TotalSeconds;
        WriteLine($"- {rate:N3} ops/s per thread");

        void Run() {
            for (var i = 0; i < opCount; i++)
                sampler.Next();
        }
    }
}
