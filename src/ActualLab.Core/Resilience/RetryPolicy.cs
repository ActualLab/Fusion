using ActualLab.Internal;

namespace ActualLab.Resilience;

/// <summary>
/// Defines the contract for a retry policy that determines when and how to
/// retry failed operations.
/// </summary>
public interface IRetryPolicy
{
    public int? TryCount { get; init; }
    public RetryDelaySeq Delays { get; init; }
    public TransiencyResolver TransiencyResolver { get; init; }
    public ExceptionFilter RetryOn { get; init; }

    public bool MustRetry(int failedTryCount);
    public bool MustRetry(Exception error, ref int failedTryCount, out Transiency transiency);

    public Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IRetryPolicy"/> implementation with configurable try count,
/// per-try timeout, delay sequence, and transiency-based filtering.
/// </summary>
public record RetryPolicy(
    int? TryCount,
    TimeSpan? TryTimeout,
    RetryDelaySeq Delays
    ) : IRetryPolicy
{
    public TransiencyResolver TransiencyResolver { get; init; } = TransiencyResolvers.PreferTransient;
    public ExceptionFilter RetryOn { get; init; } = ExceptionFilters.AnyNonTerminal;

    public RetryPolicy(RetryDelaySeq Delays)
        : this(null, null, Delays)
    { }

    public RetryPolicy(int? TryCount, RetryDelaySeq Delays)
        : this(TryCount, null, Delays)
    { }

    public RetryPolicy(TimeSpan? TryTimeout, RetryDelaySeq Delays)
        : this(null, TryTimeout, Delays)
    { }

    public virtual bool MustRetry(int failedTryCount)
        => TryCount is not { } tryCount || failedTryCount < tryCount;

    public virtual bool MustRetry(Exception error, ref int failedTryCount, out Transiency transiency)
    {
        if (!RetryOn.Invoke(error, TransiencyResolver, out transiency))
            return false;

        if (transiency is not Transiency.SuperTransient)
            ++failedTryCount;

        return MustRetry(failedTryCount);
    }

    public TimeSpan GetDelay(int failedTryCount)
        => Delays[failedTryCount];

    public async Task<T> Apply<T>(
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (TryCount <= 0)
            throw Errors.Constraint("TryCount <= 0.");

        var failedTryCount = 0;
        while (true) {
            try {
                if (!TryTimeout.HasValue)
                    return await taskFactory.Invoke(cancellationToken).ConfigureAwait(false);

                // Timeout handling
                var timeoutCts = cancellationToken.CreateLinkedTokenSource(TryTimeout.GetValueOrDefault());
                try {
                    return await taskFactory.Invoke(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                    throw new RetryPolicyTimeoutException();
                }
                finally {
                    timeoutCts.Dispose();
                }
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

                var delay = GetDelay(failedTryCount);
                retryLogger?.LogRetry(e, failedTryCount, TryCount, delay);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                else
                    await Task.Yield();
            }
        }
    }
}
