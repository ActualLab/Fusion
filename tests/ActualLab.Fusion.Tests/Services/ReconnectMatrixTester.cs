namespace ActualLab.Fusion.Tests.Services;

public interface IReconnectMatrixTester : IComputeService
{
    [ComputeMethod]
    public Task<int> Compute(
        int callKey, int delay, int invalidationDelay, CancellationToken cancellationToken = default);
    public Task<int> GetComputeInvocationCount(int callKey, CancellationToken cancellationToken = default);
    public Task<int> GetInvalidationCount(int callKey, CancellationToken cancellationToken = default);
}

public class ReconnectMatrixTester : IReconnectMatrixTester
{
    private readonly ConcurrentDictionary<int, int> _computeInvocations = new();
    private readonly ConcurrentDictionary<int, int> _invalidations = new();

    public virtual async Task<int> Compute(
        int callKey, int delay, int invalidationDelay, CancellationToken cancellationToken = default)
    {
        _computeInvocations.AddOrUpdate(callKey, 1, static (_, n) => n + 1);
        var computed = Computed.GetCurrent();
        _ = Task.Run(async () => {
            await Task.Delay(delay + invalidationDelay, CancellationToken.None).ConfigureAwait(false);
            _invalidations.AddOrUpdate(callKey, 1, static (_, n) => n + 1);
            computed.Invalidate();
        }, CancellationToken.None);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return callKey;
    }

    public virtual Task<int> GetComputeInvocationCount(int callKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_computeInvocations.GetValueOrDefault(callKey));

    public virtual Task<int> GetInvalidationCount(int callKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_invalidations.GetValueOrDefault(callKey));
}
