using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public interface IRpcBenchmarkService : IRpcService
{
    Task<long> Get(long key, CancellationToken cancellationToken);
}

public sealed class RpcBenchmarkService : IRpcBenchmarkService
{
    public Task<long> Get(long key, CancellationToken cancellationToken)
        => Task.FromResult(key);
}
