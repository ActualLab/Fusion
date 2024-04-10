using System.Runtime.ExceptionServices;
using ActualLab.Internal;

namespace ActualLab.Resilience;

public interface IRetryPolicy
{
    Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        Action<Exception, int>? errorLogger = null,
        CancellationToken cancellationToken = default);
}

public record RetryPolicy(
    int TryCount,
    TimeSpan? Timeout,
    RetryDelaySeq RetryDelays
    ) : IRetryPolicy
{
    public RetryPolicy(int TryCount, RetryDelaySeq RetryDelays)
        : this(TryCount, null, RetryDelays)
    { }

    public RetryPolicy(TimeSpan Timeout, RetryDelaySeq RetryDelays)
        : this(int.MaxValue, Timeout, RetryDelays)
    { }

    public async Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        Action<Exception, int>? errorLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (TryCount <= 0)
            throw Errors.Constraint("TryCount <= 0.");

        using var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
        var stopToken = stopTokenSource.Token;
        if (Timeout is { } maxDuration)
            stopTokenSource.CancelAfter(maxDuration);

        ExceptionDispatchInfo? lastError = null;
        var tryIndex = 0;
        while (true) {
            try {
                if (lastError != null) {
                    var delay = RetryDelays[tryIndex];
                    var delayTask = Task.Delay(delay, stopToken);
                    await delayTask.ConfigureAwait(false);
                }
                return await taskFactory.Invoke(stopToken).ConfigureAwait(false);
            }
            catch (Exception e) {
                tryIndex++;
                if (tryIndex >= TryCount)
                    throw;

                if (e.IsCancellationOf(stopToken)) {
                    if (cancellationToken.IsCancellationRequested)
                        throw;
                    lastError?.Throw();
                    throw new RetryPolicyTimeoutExceededException();
                }

                errorLogger?.Invoke(e, tryIndex);
                lastError = ExceptionDispatchInfo.Capture(e);
            }
        }
    }
}
