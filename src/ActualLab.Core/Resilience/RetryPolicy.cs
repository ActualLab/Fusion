using System.Runtime.ExceptionServices;
using ActualLab.Internal;

namespace ActualLab.Resilience;

public interface IRetryPolicy
{
    public bool MustRetry(Exception error, ref int tryCount, out Transiency transiency);
    public Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default);
}

public record RetryPolicy(
    int? TryCount,
    TimeSpan? TryTimeout,
    RetryDelaySeq RetryDelays
    ) : IRetryPolicy
{
    public TransiencyResolver TransiencyResolver { get; init; } = TransiencyResolvers.PreferTransient;
    public ExceptionFilter RetryOn { get; init; } = ExceptionFilters.AnyTransient;

    public RetryPolicy(int TryCount, RetryDelaySeq RetryDelays)
        : this(TryCount, null, RetryDelays)
    { }

    public RetryPolicy(TimeSpan TryTimeout, RetryDelaySeq RetryDelays)
        : this(int.MaxValue, TryTimeout, RetryDelays)
    { }

    public virtual bool MustRetry(Exception error, ref int tryCount, out Transiency transiency)
    {
        if (!RetryOn.Invoke(error, TransiencyResolver, out transiency))
            return false;

        if (transiency is not Transiency.SuperTransient)
            ++tryCount;
        return true;
    }

    public async Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (TryCount <= 0)
            throw Errors.Constraint("TryCount <= 0.");

        var hasTimeout = TryTimeout.HasValue;
        var tryIndex = 0;
        ExceptionDispatchInfo? lastError = null;
        while (true) {
            var timeoutCts = hasTimeout
                ? cancellationToken.CreateLinkedTokenSource(TryTimeout.GetValueOrDefault())
                : null;
            var timeoutToken = timeoutCts?.Token ?? cancellationToken;
            try {
                return await taskFactory.Invoke(timeoutToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                if (!MustRetry(e, ref tryIndex, out var transiency)) {
                    var reason = transiency is Transiency.Terminal ? "terminal error" : "non-transient error";
                    retryLogger?.LogError(e, tryIndex, TryCount, reason);
                    throw;
                }
                if (tryIndex >= TryCount) {
                    retryLogger?.LogError(e, tryIndex, TryCount, "no more retries");
                    throw;
                }
                if (hasTimeout && e.IsCancellationOf(timeoutToken)) {
                    lastError?.Throw();
                    throw new RetryPolicyTimeoutExceededException();
                }

                lastError = ExceptionDispatchInfo.Capture(e);
                var delay = RetryDelays[Math.Max(1, tryIndex)];
                retryLogger?.LogRetry(e, tryIndex, TryCount, delay);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally {
                timeoutCts?.Dispose();
            }
        }
    }
}
