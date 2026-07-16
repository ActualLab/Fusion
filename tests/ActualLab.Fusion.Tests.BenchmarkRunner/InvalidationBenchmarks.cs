using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[WarmupCount(8), IterationCount(10)]
public class RawInvalidationBenchmarks : FusionBenchmarkBase
{
    [IterationSetup]
    public void Prepare()
    {
        for (var batch = 0; batch < BenchmarkSettings.InvalidationBatchCount; batch++) {
            var keyOffset = (long)batch * BenchmarkSettings.OperationCount;
            for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
                _ = Service.Get(keyOffset + i, default).AssertCompleted();
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount * BenchmarkSettings.InvalidationBatchCount)]
    public void Invalidate()
    {
        using var invalidationScope = Invalidation.Begin();
        for (var batch = 0; batch < BenchmarkSettings.InvalidationBatchCount; batch++) {
            var keyOffset = (long)batch * BenchmarkSettings.OperationCount;
            for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
                _ = Service.Get(keyOffset + i, default).AssertCompleted();
        }
    }
}

[WarmupCount(8), IterationCount(10)]
public class RegisteredComputedInvalidationBenchmarks : FusionBenchmarkBase
{
    private Computed[] _computeds = [];

    [IterationSetup]
    public void Prepare()
    {
        _computeds = new Computed[BenchmarkSettings.OperationCount * BenchmarkSettings.InvalidationBatchCount];
        for (var i = 0; i < _computeds.Length; i++)
            _computeds[i] = Computed.Capture(() => Service.Get(i, default)).AssertCompleted().Result;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount * BenchmarkSettings.InvalidationBatchCount)]
    public void Invalidate()
    {
        foreach (var computed in _computeds)
            computed.Invalidate(immediately: true, InvalidationSource.Unknown);
    }
}

public class InvalidationCascadeBenchmarks : FusionBenchmarkBase
{
    public static IEnumerable<TreeShape> TreeShapes
        => new[] { TreeShape.New(2), TreeShape.New(3), TreeShape.New(4) };

    [ParamsSource(nameof(TreeShapes))]
    public TreeShape Shape { get; set; } = null!;

    [IterationSetup]
    public void Prepare()
    {
        for (var nodeId = 0L; nodeId < Shape.NodeCount; nodeId++)
            _ = Service.GetNode(Shape.BranchingFactor, nodeId, default).AssertCompleted();
    }

    [Benchmark]
    public void InvalidateRoot()
    {
        using var invalidationScope = Invalidation.Begin();
        _ = Service.GetNode(Shape.BranchingFactor, 0, default).AssertCompleted();
    }
}

[MemoryDiagnoser, WarmupCount(8), IterationCount(10)]
public class DirectInvalidationCascadeBenchmarks
{
    private const int TreeCount = 1024;

    private Computed[] _roots = [];

    public static IEnumerable<TreeShape> TreeShapes => InvalidationCascadeBenchmarks.TreeShapes;

    [ParamsSource(nameof(TreeShapes))]
    public TreeShape Shape { get; set; } = null!;

    [IterationSetup]
    public void Prepare()
    {
        _roots = new Computed[TreeCount];
        for (var treeIndex = 0; treeIndex < _roots.Length; treeIndex++) {
            var nodes = new Computed[Shape.NodeCount];
            nodes[0] = Computed.New(_ => TaskExt.UnitTask).Update().AssertCompleted().Result;
            for (var nodeIndex = 1; nodeIndex < nodes.Length; nodeIndex++) {
                var parent = nodes[(nodeIndex - 1) / Shape.BranchingFactor];
                nodes[nodeIndex] = Computed.New(async ct => {
                    await parent.UseUntyped(ct).ConfigureAwait(false);
                    return default(Unit);
                }).Update().AssertCompleted().Result;
            }
            _roots[treeIndex] = nodes[0];
        }
    }

    [Benchmark(OperationsPerInvoke = TreeCount)]
    public void InvalidateRoot()
    {
        foreach (var root in _roots)
            root.Invalidate(immediately: true, InvalidationSource.Unknown);
    }
}

public sealed record TreeShape(int BranchingFactor, int NodeCount)
{
    public static TreeShape New(int branchingFactor)
    {
        var nodeCount = 1;
        var levelSize = 1;
        while (nodeCount + levelSize * branchingFactor <= BenchmarkSettings.OperationCount) {
            levelSize *= branchingFactor;
            nodeCount += levelSize;
        }
        return new TreeShape(branchingFactor, nodeCount);
    }

    public override string ToString()
        => $"K={BranchingFactor}, N={NodeCount}";
}
