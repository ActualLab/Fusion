namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public interface IBenchmarkComputeService : IComputeService
{
    [ComputeMethod]
    Task<Unit> Get(long key, CancellationToken cancellationToken);
    [ComputeMethod]
    Task<Unit> Get(string key, CancellationToken cancellationToken);
    [ComputeMethod]
    Task<Unit> Get(Session session, string key, CancellationToken cancellationToken);
    [ComputeMethod]
    Task<Unit> GetNode(int branchingFactor, long nodeId, CancellationToken cancellationToken);
}

public class BenchmarkComputeService : IBenchmarkComputeService
{
    [ComputeMethod]
    public virtual Task<Unit> Get(long key, CancellationToken cancellationToken)
        => TaskExt.UnitTask;

    [ComputeMethod]
    public virtual Task<Unit> Get(string key, CancellationToken cancellationToken)
        => TaskExt.UnitTask;

    [ComputeMethod]
    public virtual Task<Unit> Get(Session session, string key, CancellationToken cancellationToken)
        => TaskExt.UnitTask;

    [ComputeMethod]
    public virtual async Task<Unit> GetNode(
        int branchingFactor,
        long nodeId,
        CancellationToken cancellationToken)
    {
        if (nodeId > 0) {
            var parentId = (nodeId - 1) / branchingFactor;
            await GetNode(branchingFactor, parentId, cancellationToken).ConfigureAwait(false);
        }
        return default;
    }
}
