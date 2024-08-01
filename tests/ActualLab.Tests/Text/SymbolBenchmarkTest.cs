namespace ActualLab.Tests.Text;

public sealed class SymbolBenchmarkTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly int[] DefaultOptions = [12, 20, 32, 64];
    private static readonly int DefaultIterationCount = 20_000_000;

    [Fact]
    public async Task CreationTest()
    {
        var options = DefaultOptions;
        var iterationCount = DefaultIterationCount;
        await Benchmark("new Symbol(length) - pure overhead", iterationCount, static (c, n) => {
            var s = new string('0', c);
            for (var i = 0; i < n; i++)
                _ = new Symbol(s);
        }, options);
        await Benchmark("new string(length)", iterationCount, static (c, n) => {
            var s = new string('0', c).AsSpan();
            for (var i = 0; i < n; i++)
                _ = s.ToString();
        }, options);
        await Benchmark("new string(length).ToSymbol()", iterationCount, static (c, n) => {
            var s = new string('0', c).AsSpan();
            for (var i = 0; i < n; i++)
                _ = (Symbol)s.ToString();
        }, options);
    }

    [Fact]
    public async Task HashCodeTest()
    {
        var options = DefaultOptions;
        var iterationCount = DefaultIterationCount * 2;
        await Benchmark("Symbol(length).GetHashCode()", iterationCount, static (c, n) => {
            var s = (Symbol)new string('0', c);
            while (--n > 0)
                _ = s.GetHashCode();
        }, options);
        await Benchmark("(object)Symbol(length).GetHashCode()", iterationCount, static (c, n) => {
            var s = (object)(Symbol)new string('0', c);
            while (--n > 0)
                _ = s.GetHashCode();
        }, 5, 10, 20);

        await Benchmark("string(length).GetHashCode(StringComparison.Ordinal)", iterationCount, static (c, n) => {
            var s = new string('0', c);
            while (--n > 0)
#pragma warning disable MA0021
                _ = s.GetHashCode();
#pragma warning restore MA0021
        }, options);
        await Benchmark("(object)string(length).GetHashCode()", iterationCount, static (c, n) => {
            var s = (object)new string('0', c);
            while (--n > 0)
                _ = s.GetHashCode();
        }, 5, 10, 20);
    }

    [Fact]
    public async Task EqualsTest()
    {
        var options = DefaultOptions;
        var iterationCount = DefaultIterationCount / 2;
        await Benchmark("Symbol(length) == Symbol(length) - when ==", iterationCount, static (c, n) => {
            var s1 = (Symbol)new string('0', c);
            var s2 = (Symbol)new string('0', c);
            while (--n > 0)
                _ = s1 == s2;
        }, options);
        await Benchmark("Symbol(length) == Symbol(length) - when !=", iterationCount, static (c, n) => {
            var s1 = (Symbol)new string('0', c);
            var s2 = (Symbol)new string('1', c);
            while (--n > 0)
                _ = s1 == s2;
        }, options);

        await Benchmark("string(length) == string(length) - when ==", iterationCount, static (c, n) => {
            var s1 = new string('0', c);
            var s2 = new string('0', c);
            while (--n > 0)
                _ = Equals(s1, s2);
        }, options);
        await Benchmark("string(length) == string(length) - when !=", iterationCount, static (c, n) => {
            var s1 = new string('0', c);
            var s2 = new string('1', c);
            while (--n > 0)
                _ = Equals(s1, s2);
        }, options);
    }

    [Fact]
    public async Task DictionaryLookupTest()
    {
        var sizeOptions = new[] { 10_000 };
        var lengthOptions = DefaultOptions;
        var options = sizeOptions
            .SelectMany(size => lengthOptions.Select(length => (size, length)))
            .ToArray();
        var iterationCount = DefaultIterationCount;
        await Benchmark("Dictionary<Symbol, Unit>(size).TryGetValue(Symbol(length)) - hit", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => (Symbol)string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit));
            var key = d.Skip(size / 2).FirstOrDefault().Key;
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);
        await Benchmark("Dictionary<Symbol, Unit>(size).TryGetValue(Symbol(length)) - miss", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => (Symbol)string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit));
            var key = (Symbol)"x";
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);

        await Benchmark("Dictionary<Symbol, Unit>(size).TryGetValue(string(length)) - hit", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => (Symbol)string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit));
            var key = d.Skip(size / 2).FirstOrDefault().Key.Value;
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);
        await Benchmark("Dictionary<Symbol, Unit>(size).TryGetValue(string(length)) - miss", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => (Symbol)string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit));
            var key = "x";
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);

        await Benchmark("Dictionary<string, Unit>(size).TryGetValue(string(length)) - hit", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit), StringComparer.Ordinal);
            var key = d.Skip(size / 2).FirstOrDefault().Key;
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);
        await Benchmark("Dictionary<string, Unit>(size).TryGetValue(string(length)) - miss", iterationCount, static (options, n) => {
            var (size, length) = options;
            var d = Enumerable.Range(0, size)
                .Select(n => string.Format($"{{0,-{length}}}", n))
                .ToDictionary(x => x, _ => default(Unit), StringComparer.Ordinal);
            var key = "x";
            var startedAt = CpuTimestamp.Now;
            while (--n > 0)
                _ = d.TryGetValue(key, out _);
            return startedAt;
        }, options);
    }

    // Private methods

    private Task Benchmark<T>(
        string title, int iterationCount, Action<T, int> action,
        params T[] options)
        where T : notnull
        => Benchmark(title, iterationCount, (option, count) => {
            var startedAt = CpuTimestamp.Now;
            action.Invoke(option, count);
            return startedAt;
        }, options);
    private async Task Benchmark<T>(
        string title, int iterationCount, Func<T, int, CpuTimestamp> action,
        params T[] options)
        where T : notnull
    {
        Out.WriteLine($"{title}:");
        var maxOptionWidth = options.Select(o => o.ToString()!.Length).Max();
        foreach (var option in options) {
            var frequency = 0d;
            for (var testIndex = 0; testIndex < 5; testIndex++) {
                GC.Collect();
                await Task.Delay(100);
                GC.Collect();

                var startedAt = action.Invoke(option, iterationCount);
                frequency = Math.Max(frequency, iterationCount / startedAt.Elapsed.TotalSeconds);
            }

            var (f, suffix) = frequency switch {
                >= 2e8 => (frequency / 1e9, "G"),
                >= 2e5 => (frequency / 1e6, "M"),
                >= 2e2 => (frequency / 1e9, "K"),
                _ => (frequency, "")
            };
            var sOption = string.Format($"{{0,-{maxOptionWidth}}}", option);
            Out.WriteLine($"  {sOption}: {f:F2}{suffix} ops/s");
        }
    }
}
