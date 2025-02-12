using ActualLab.Concurrency;
using ActualLab.Tests.Caching.Alternative;

namespace ActualLab.Tests.Caching;

public class GCHandlePoolBenchmark(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private const int IterationCount = 10_000_000;

    [Fact]
    public async Task DirectBenchmark()
    {
        await Benchmark("GCHandle.Alloc()*N + Free()*N", IterationCount, n => {
            var o = new object();
            var handles = new GCHandle[n];
            for (var i = 0; i < n; i++)
                handles[i] = GCHandle.Alloc(o, GCHandleType.Weak);
            for (var i = 0; i < n; i++)
                handles[i].Free();
        });

        await Benchmark("(GCHandle.Alloc(), Free())*N", IterationCount, n => {
            var o = new object();
            for (var i = 0; i < n; i++) {
                var h = GCHandle.Alloc(o, GCHandleType.Weak);
                h.Free();
            }
        });
    }

    [Fact]
    public async Task PoolBenchmark()
    {
        await Benchmark("GCHandlePool.Acquire()*N + Release()*N", IterationCount, n => {
            var o = new object();
            var pool = new GCHandlePool(GCHandleType.Weak);
            var handles = new GCHandle[n];
            for (var i = 0; i < n; i++)
                handles[i] = pool.Acquire(o, i);
            for (var i = 0; i < n; i++)
                pool.Release(handles[i], i);
        });

        await Benchmark("(GCHandlePool.Acquire(), Release())*N", IterationCount, n => {
            var o = new object();
            var pool = new GCHandlePool(GCHandleType.Weak);
            for (var i = 0; i < n; i++) {
                var h = pool.Acquire(o, i);
                pool.Release(h, i);
            }
        });
    }
}
