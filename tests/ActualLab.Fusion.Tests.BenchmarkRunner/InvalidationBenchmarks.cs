using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public class RawInvalidationBenchmarks : FusionBenchmarkBase
{
    [IterationSetup]
    public void Prepare()
    {
        for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
            _ = Service.Get(i, default).AssertCompleted();
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public void Invalidate()
    {
        using var invalidationScope = Invalidation.Begin();
        for (var i = 0L; i < BenchmarkSettings.OperationCount; i++)
            _ = Service.Get(i, default).AssertCompleted();
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
