namespace ActualLab.Fusion.Tests.Services;

public interface IReconnectTester : IComputeService
{
    [ComputeMethod]
    public Task<(int, int)> Delay(int delay, int invalidationDelay, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<Moment> GetTime(CancellationToken cancellationToken = default);
}

public class ReconnectTester : IReconnectTester
{
    public virtual async Task<(int, int)> Delay(int delay, int invalidationDelay, CancellationToken cancellationToken = default)
    {
        var computed = Computed.GetCurrent();
        _ = Task.Run(async () => {
            await Task.Delay(delay + invalidationDelay, CancellationToken.None).ConfigureAwait(false);
            computed.Invalidate();
        }, CancellationToken.None);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return (delay, invalidationDelay);
    }

    public virtual Task<Moment> GetTime(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public void InvalidateGetTime()
    {
        using var scope = Invalidation.Begin();
        _ = GetTime();
    }
}
