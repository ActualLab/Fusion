using System.Runtime.ExceptionServices;
using ActualLab.Internal;

namespace ActualLab.Resilience;

public interface IRetryPolicy
{
    bool MustRetry(Exception error, out Transiency transiency);
    Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default);
}

public record RetryPolicy(
    int TryCount,
    TimeSpan? TryTimeout,
    RetryDelaySeq RetryDelays
    ) : IRetryPolicy
{
    public TransiencyResolver TransiencyResolver { get; init; } = TransiencyResolvers.PreferTransient;
    public bool RetryOnNonTransient { get; init; } = false;

    public RetryPolicy(int TryCount, RetryDelaySeq RetryDelays)
        : this(TryCount, null, RetryDelays)
    { }

    public RetryPolicy(TimeSpan TryTimeout, RetryDelaySeq RetryDelays)
        : this(int.MaxValue, TryTimeout, RetryDelays)
    { }

    public bool MustRetry(Exception error, out Transiency transiency)
    {
        transiency = TransiencyResolver.Invoke(error);
        return transiency.MustRetry(RetryOnNonTransient);
    }

    public async Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (TryCount <= 0)
            throw Errors.Constraint("TryCount <= 0.");

        ExceptionDispatchInfo? lastError = null;
        var tryIndex = 0;
        while (true) {
            using var timeoutCts = cancellationToken.CreateLinkedTokenSource(TryTimeout);
            var timeoutToken = timeoutCts.Token;
            try {
                return await taskFactory.Invoke(timeoutToken).ConfigureAwait(false);
            }
            catch (Exception e) {
                if (!MustRetry(e, out var transiency)) {
                    var reason = transiency.IsTerminal() ? "terminal error" : "non-transient error";
                    retryLogger?.LogError(e, tryIndex, TryCount, reason);
                    throw;
                }

                if (!transiency.IsSuperTransient())
                    tryIndex++;
                if (tryIndex >= TryCount) {
                    retryLogger?.LogError(e, tryIndex, TryCount, "no more retries");
                    throw;
                }

                if (e.IsCancellationOf(timeoutToken)) {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    lastError?.Throw();
                    throw new RetryPolicyTimeoutExceededException();
                }

                lastError = ExceptionDispatchInfo.Capture(e);
                var delay = RetryDelays[Math.Max(1, tryIndex)];
                retryLogger?.LogRetry(e, tryIndex, TryCount, delay);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
