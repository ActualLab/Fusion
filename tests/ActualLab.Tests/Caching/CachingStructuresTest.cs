using ActualLab.Tests.Caching.Alternative;

namespace ActualLab.Tests.Caching;

public class CachingStructuresTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int[] DefaultVariants = [0, 1, 3, 5, 10, 20, 30, 50, 100, 200, 500, 1000, 10_000];

    [Fact]
    public void GrowOnlyCacheTest()
    {
        var rnd = new Random(1);
        for (var size = 0; size < 200; size++) {
            var c = GrowOnlyCache<int, int>.New(CollidingKeyComparer<int>.Instance);
            for (var i = 0; i < size; i++) {
                c.TryGetValue(i, out _).Should().BeFalse();
                if (rnd.NextDouble() < 0.5)
                    c.GetOrAdd(ref c, i, x => x);
                else
                    c.GetOrAdd(ref c, i, (x, _) => x, 0);
                c.TryGetValue(i, out var j).Should().BeTrue();
                j.Should().Be(i);
            }
        }
    }

    [Fact]
    public async Task LookupBenchmark()
    {
        var iterationCount = 1000_000;
        var source = Enumerable.Range(0, 10_000).Select(x => new KeyValuePair<int, int>(x, x)).ToArray();

        await Benchmark("GrowOnlyCache<int, int>.TryGetValue()", iterationCount, (length, n) => {
            var d = GrowOnlyCache<int, int>.New(source.Take(length));
            var j = 0;
            for (var i = 0; i < n; i++) {
                d.TryGetValue(j++, out _);
                if (j >= length)
                    j = 0;
            }
        }, DefaultVariants);

        await Benchmark("Dictionary<int, int>.TryGetValue()", iterationCount, (length, n) => {
            var d = source.Take(length).ToDictionary(x => x.Key);
            var j = 0;
            for (var i = 0; i < n; i++) {
                d.TryGetValue(j++, out _);
                if (j >= length)
                    j = 0;
            }
        }, DefaultVariants);

        await Benchmark("ConcurrentDictionary<int, int>.TryGetValue()", iterationCount, (length, n) => {
            var d = new ConcurrentDictionary<int, int>(source.Take(length));
            var j = 0;
            for (var i = 0; i < n; i++) {
                d.TryGetValue(j++, out _);
                if (j >= length)
                    j = 0;
            }
        }, DefaultVariants);
    }

    // Nested types

    private class CollidingKeyComparer<T> : IEqualityComparer<T>
    {
        public static readonly IEqualityComparer<T> Instance = new CollidingKeyComparer<T>();

        private readonly IEqualityComparer<T> _baseComparer = EqualityComparer<T>.Default;

        public bool Equals(T? x, T? y)
            => _baseComparer.Equals(x!, y!);

        public int GetHashCode(T obj)
            => _baseComparer.GetHashCode(obj!) & 1;
    }
}
