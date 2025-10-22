using ActualLab.Pooling;

namespace ActualLab.Tests.Pooling;

public class WeakReferenceSlimBenchmark(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private const int IterationCount = 10_000_000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseBenchmark(bool runGCCollect)
    {
        Out.WriteLine($"Run GC collect in each test: {runGCCollect}");
        var o = "test object";

        GCHandle[] handles = null!;
        await Benchmark("GCHandle.Weak: (Alloc, Free)*N", IterationCount, n => {
            handles = new GCHandle[n];
            for (var i = 0; i < n; i++) {
                var h = GCHandle.Alloc(o, GCHandleType.Weak);
                handles[i] = h;
                h.Free();
            }

            if (runGCCollect) {
                handles = null!;
                GC.Collect();
            }
        });

        WeakReferenceSlim<string>[] slimWeakRefs = null!;
        await Benchmark("WeakReferenceSlim: (new only)*N", IterationCount, n => {
            slimWeakRefs = new WeakReferenceSlim<string>[n];
            for (var i = 0; i < n; i++) {
                var hr = new WeakReferenceSlim<string>(o);
                slimWeakRefs[i] = hr;
                hr.Dispose();
            }

            if (runGCCollect) {
                slimWeakRefs = null!;
                GC.Collect();
            }
        });

        WeakReference<string>[] weakRefs = null!;
        await Benchmark("WeakReference: (new, destroy)*N", IterationCount, n => {
            weakRefs = new WeakReference<string>[n];
            for (var i = 0; i < n; i++) {
                var wr = new WeakReference<string>(o);
                weakRefs[i] = wr;
            }

            if (runGCCollect) {
                weakRefs = null!;
                GC.Collect();
            }
        });
    }
}
