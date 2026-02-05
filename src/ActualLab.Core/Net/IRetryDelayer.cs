namespace ActualLab.Net;

/// <summary>
/// Defines the contract for computing retry delays based on the attempt index.
/// </summary>
public interface IRetryDelayer
{
    public MomentClock Clock { get; }
    public CancellationToken CancelDelaysToken { get; }

    public RetryDelay GetDelay(int tryIndex, CancellationToken cancellationToken = default);
    public void CancelDelays();
}
