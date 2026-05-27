using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public interface IReconnectMatrixRpcTester : IRpcService
{
    public Task<int> Rpc(int callKey, int delay, CancellationToken cancellationToken = default);
    public Task<int> GetInvocationCount(int callKey, CancellationToken cancellationToken = default);
}

public class ReconnectMatrixRpcTester : IReconnectMatrixRpcTester
{
    private readonly ConcurrentDictionary<int, int> _invocations = new();

    public virtual async Task<int> Rpc(int callKey, int delay, CancellationToken cancellationToken = default)
    {
        _invocations.AddOrUpdate(callKey, 1, static (_, n) => n + 1);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return callKey;
    }

    public virtual Task<int> GetInvocationCount(int callKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_invocations.GetValueOrDefault(callKey));
}
