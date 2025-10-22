using ActualLab.Pooling;

namespace ActualLab.Tests.Pooling;

public class WeakReferenceSlimBenchmark(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private const int IterationCount = 10_000_000;

    [Fact]
    public async Task UseBenchmark()
    {
        var o = "test object";

        GCHandle[] handles = null!;
        await Benchmark("GCHandle.Weak: (Alloc, Free)*N", IterationCount, n => {
            handles = new GCHandle[n];
            for (var i = 0; i < n; i++) {
                var h = GCHandle.Alloc(o, GCHandleType.Weak);
                handles[i] = h;
                h.Free();
            }
        });

        WeakReferenceSlim<string>[] slimWeakRefs = null!;
        await Benchmark("WeakReferenceSlim: (new, Dispose)*N", IterationCount, n => {
            slimWeakRefs = new WeakReferenceSlim<string>[n];
            for (var i = 0; i < n; i++) {
                var hr = new WeakReferenceSlim<string>(o);
                slimWeakRefs[i] = hr;
                hr.Dispose();
            }
        });

        WeakReference<string>[] weakRefs = null!;
        await Benchmark("WeakReference: (new)*N", IterationCount, n => {
            weakRefs = new WeakReference<string>[n];
            for (var i = 0; i < n; i++) {
                var wr = new WeakReference<string>(o);
                weakRefs[i] = wr;
            }
        });
    }
}
