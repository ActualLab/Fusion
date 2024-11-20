namespace ActualLab.Net;

public interface IRetryDelayer
{
    public MomentClock Clock { get; }
    public CancellationToken CancelDelaysToken { get; }

    public RetryDelay GetDelay(int tryIndex, CancellationToken cancellationToken = default);
    public void CancelDelays();
}
