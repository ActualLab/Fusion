using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public interface ITypeScriptTestComputeService : IComputeService
{
    Task<int> Add(int a, int b, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> GetCounter(string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> GetValue(int value, CancellationToken cancellationToken = default);
    Task Set(string key, int value, CancellationToken cancellationToken = default);
    Task Increment(string key, CancellationToken cancellationToken = default);
    Task<int> GetCounterNonCompute(string key, CancellationToken cancellationToken = default);
    Task<RpcStream<int>> StreamInt32(int count);
}
