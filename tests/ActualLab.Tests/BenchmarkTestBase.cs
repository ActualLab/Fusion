namespace ActualLab.Tests;

public abstract class BenchmarkTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected int TryCount { get; set; } = 5;
    protected TimeSpan InterTryDelay { get; set; } = TimeSpan.FromSeconds(0.1);

    // Private methods

    protected Task Benchmark(string title, int iterationCount, Action<int> action)
        => Benchmark(title, iterationCount, (_, c) => action.Invoke(c), "Default");

    protected Task Benchmark(string title, int iterationCount, Func<int, CpuTimestamp> func)
        => Benchmark(title, iterationCount, (_, c) => func.Invoke(c), "Default");

    protected Task Benchmark<T>(
        string title, int iterationCount, Action<T, int> action,
        params T[] variants)
        where T : notnull
        => Benchmark(title, iterationCount, (option, count) => {
            var startedAt = CpuTimestamp.Now;
            action.Invoke(option, count);
            return startedAt;
        }, variants);

    protected async Task Benchmark<T>(
        string title, int iterationCount, Func<T, int, CpuTimestamp> func,
        params T[] variants)
        where T : notnull
    {
        Out.WriteLine($"{title}:");
        var maxVariantWidth = variants.Select(o => o.ToString()!.Length).Max();
        foreach (var variant in variants) {
            var frequency = 0d;
            for (var testIndex = 0; testIndex < TryCount; testIndex++) {
                GC.Collect();
                await Task.Delay(InterTryDelay);
                GC.Collect();

                var startedAt = func.Invoke(variant, iterationCount);
                frequency = Math.Max(frequency, iterationCount / startedAt.Elapsed.TotalSeconds);
            }

            var (f, suffix) = frequency switch {
                >= 2e8 => (frequency / 1e9, "G"),
                >= 2e5 => (frequency / 1e6, "M"),
                >= 2e2 => (frequency / 1e9, "K"),
                _ => (frequency, "")
            };
            var sVariant = string.Format($"{{0,-{maxVariantWidth}}}", variant);
            Out.WriteLine($"  {sVariant}: {f:F3}{suffix} ops/s");
        }
    }
}
