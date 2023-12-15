namespace ActualLab.Net;

public interface IRetryDelayer
{
    IMomentClock Clock { get; }
    CancellationToken CancelDelaysToken { get; }

    RetryDelay GetDelay(int tryIndex, CancellationToken cancellationToken = default);
    void CancelDelays();
}
