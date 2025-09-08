using System.Runtime.ExceptionServices;
using ActualLab.Internal;

namespace ActualLab.Resilience;

public interface IRetryPolicy
{
    public bool MustRetry(Exception error, ref int failedTryCount, out Transiency transiency);
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

    public RetryPolicy(RetryDelaySeq RetryDelays)
        : this(null, null, RetryDelays)
    { }

    public RetryPolicy(int? TryCount, RetryDelaySeq RetryDelays)
        : this(TryCount, null, RetryDelays)
    { }

    public RetryPolicy(TimeSpan? TryTimeout, RetryDelaySeq RetryDelays)
        : this(null, TryTimeout, RetryDelays)
    { }

    public bool MustRetry(int failedTryCount)
        => TryCount is not { } tryCount || failedTryCount < tryCount;

    public virtual bool MustRetry(Exception error, ref int failedTryCount, out Transiency transiency)
    {
        if (!RetryOn.Invoke(error, TransiencyResolver, out transiency))
            return false;

        if (transiency is not Transiency.SuperTransient)
            ++failedTryCount;

        return MustRetry(failedTryCount);
    }

    public async Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (TryCount <= 0)
            throw Errors.Constraint("TryCount <= 0.");

        var hasTimeout = TryTimeout.HasValue;
        var failedTryCount = 0;
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
                if (!MustRetry(e, ref failedTryCount, out var transiency)) {
                    var reason = MustRetry(failedTryCount)
                        ? transiency is Transiency.Terminal
                            ? "terminal error"
                            : "non-retriable error"
                        : "no more retries";
                    retryLogger?.LogError(e, failedTryCount, TryCount, reason);
                    throw;
                }
                if (hasTimeout && e.IsCancellationOf(timeoutToken)) {
                    lastError?.Throw();
                    throw new RetryPolicyTimeoutExceededException();
                }

                lastError = ExceptionDispatchInfo.Capture(e);
                var delay = RetryDelays[Math.Max(1, failedTryCount)];
                retryLogger?.LogRetry(e, failedTryCount, TryCount, delay);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally {
                timeoutCts?.Dispose();
            }
        }
    }
}
