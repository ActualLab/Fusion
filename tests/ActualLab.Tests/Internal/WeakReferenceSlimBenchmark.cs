namespace ActualLab.Tests.Internal;

public class WeakReferenceSlimBenchmark(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private const int IterationCount = 1_000_000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseBenchmark(bool runGCCollect)
    {
        Out.WriteLine($"Run GC collect in each test: {runGCCollect}");
        var o = "test object";
        var handles = new GCHandle[IterationCount];
        var slimWeakRefs = new WeakReferenceSlim<string>[IterationCount];
        var weakRefs = new WeakReference<string>[IterationCount];

        await Benchmark("GCHandle.Weak: (Alloc, Free)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var h = GCHandle.Alloc(o, GCHandleType.Weak);
                handles[i] = h;
            }
            for (var i = 0; i < n; i++)
                handles[i].Free();

            if (runGCCollect) {
                handles.AsSpan().Clear();
                GC.Collect();
            }
        });

        // await Timeouts.Generic5S.FireImmediately();
        await Benchmark("WeakReferenceSlim: (new only)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var wr = new WeakReferenceSlim<string>(o);
                slimWeakRefs[i] = wr;
                wr.Dispose();
            }

            if (runGCCollect) {
                // Timeouts.Generic5S.FireImmediately().Wait();
                slimWeakRefs.AsSpan().Clear();
                GC.Collect();
            }
        });
        // await Timeouts.Generic5S.FireImmediately();

        await Benchmark("WeakReference: (new, destroy)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var wr = new WeakReference<string>(o);
                weakRefs[i] = wr;
            }

            if (runGCCollect) {
                weakRefs.AsSpan().Clear();
                GC.Collect();
            }
        });
    }
}
