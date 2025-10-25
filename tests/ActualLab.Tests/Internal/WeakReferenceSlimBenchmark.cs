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
        object? target = null;
        var handles = new GCHandle[IterationCount];
        var slimWeakRefs = new WeakReferenceSlim<string>[IterationCount];
        var weakRefs = new WeakReference<string>[IterationCount];

        await Benchmark("GCHandle.Weak: (Alloc, Target, Free)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var h = GCHandle.Alloc(o, GCHandleType.Weak);
                handles[i] = h;
                target = h.Target;
                handles[i].Free();
            }

            if (runGCCollect) {
                handles.AsSpan().Clear();
                GC.Collect();
            }
        });

        await Benchmark("WeakReferenceSlim: (new, Target, Dispose)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var wr = new WeakReferenceSlim<string>(o);
                slimWeakRefs[i] = wr;
                target = wr.Target;
                wr.Dispose();
            }

            if (runGCCollect) {
                slimWeakRefs.AsSpan().Clear();
                GC.Collect();
            }
        });

        await Benchmark("WeakReference: (new, Target, maybe ~Finalize)*N", IterationCount, n => {
            for (var i = 0; i < n; i++) {
                var wr = new WeakReference<string>(o);
                wr.TryGetTarget(out var s);
                target = s;
                weakRefs[i] = wr;
            }

            if (runGCCollect) {
                weakRefs.AsSpan().Clear();
                GC.Collect();
            }
        });

        // Just to use target
        Out.WriteLine($"{target}"[..0]);
    }
}
