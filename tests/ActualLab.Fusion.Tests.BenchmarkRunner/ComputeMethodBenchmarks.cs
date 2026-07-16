using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public class CachedComputeMethodBenchmarks : FusionBenchmarkBase
{
    private const string Key = "benchmark";
    private readonly Session _session = new("benchmark-session");

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public Task<Unit> Long()
    {
        Task<Unit> result = null!;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            result = Service.Get(0L, default);
        return result;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public Task<Unit> String()
    {
        Task<Unit> result = null!;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            result = Service.Get(Key, default);
        return result;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public Task<Unit> SessionAndString()
    {
        Task<Unit> result = null!;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            result = Service.Get(_session, Key, default);
        return result;
    }

    protected override void OnSetup()
    {
        _ = Service.Get(0L, default).AssertCompleted();
        _ = Service.Get(Key, default).AssertCompleted();
        _ = Service.Get(_session, Key, default).AssertCompleted();
    }
}

public class RecomputeComputeMethodBenchmarks : FusionBenchmarkBase
{
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public Task<Unit> Recompute()
    {
        Task<Unit> result = null!;
        for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
            result = Service.Get(i, default);
        return result;
    }

    [IterationCleanup]
    public void InvalidateCycle()
    {
        using var invalidationScope = Invalidation.Begin();
        for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
            _ = Service.Get(i, default).AssertCompleted();
    }
}
