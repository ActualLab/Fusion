namespace ActualLab.Resilience;

#pragma warning disable MA0100

/// <summary>
/// Extension methods for <see cref="IRetryPolicy"/>.
/// </summary>
public static class RetryPolicyExt
{
    // MustRetry overloads

    public static bool MustRetry(this IRetryPolicy policy, Exception error)
        => policy.MustRetry(error, 0);

    public static bool MustRetry(this IRetryPolicy policy, Exception error, out Transiency transiency)
        => policy.MustRetry(error, 0, out transiency);

    public static bool MustRetry(this IRetryPolicy policy, Exception error, int failedTryCount)
        => policy.MustRetry(error, ref failedTryCount, out _);

    public static bool MustRetry(this IRetryPolicy policy, Exception error, int failedTryCount, out Transiency transiency)
        => policy.MustRetry(error, ref failedTryCount, out transiency);

    public static bool MustRetry(this IRetryPolicy policy, Exception error, ref int failedTryCount)
        => policy.MustRetry(error, ref failedTryCount, out _);

    // Apply overloads

    public static Task<T> Apply<T>(
        this IRetryPolicy policy,
        Func<CancellationToken, Task<T>> taskFactory,
        CancellationToken cancellationToken = default)
        => policy.Apply(taskFactory, null, cancellationToken);

    public static Task Apply(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
        => policy.Apply(taskFactory, null, cancellationToken);

    public static Task Apply(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        RetryLogger? retryLogger,
        CancellationToken cancellationToken = default)
    {
        return policy.Apply(UnitTaskFactory, retryLogger, cancellationToken);

        async Task<Unit> UnitTaskFactory(CancellationToken cancellationToken1)
        {
            await taskFactory.Invoke(cancellationToken1).ConfigureAwait(false);
            return default;
        }
    }

    // Run overloads

    public static Task<T> Run<T>(
        this IRetryPolicy policy,
        Func<CancellationToken, Task<T>> taskFactory,
        CancellationToken cancellationToken = default)
        => Task.Run(() => policy.Apply(taskFactory, null, cancellationToken), cancellationToken);

    public static Task<T> Run<T>(
        this IRetryPolicy policy,
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => policy.Apply(taskFactory, retryLogger, cancellationToken), cancellationToken);

    public static Task Run(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
        => Task.Run(() => policy.Apply(taskFactory, null, cancellationToken), cancellationToken);

    public static Task Run(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        RetryLogger? retryLogger,
        CancellationToken cancellationToken = default)
        => Task.Run(() => policy.Apply(taskFactory, retryLogger, cancellationToken), cancellationToken);

    // RunIsolated overloads

    public static Task<T> RunIsolated<T>(
        this IRetryPolicy policy,
        Func<CancellationToken, Task<T>> taskFactory,
        CancellationToken cancellationToken = default)
    {
        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Task.Run(() => policy.Apply(taskFactory, null, cancellationToken), cancellationToken);
    }

    public static Task<T> RunIsolated<T>(
        this IRetryPolicy policy,
        Func<CancellationToken, Task<T>> taskFactory,
        RetryLogger? retryLogger = null,
        CancellationToken cancellationToken = default)
    {
        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Task.Run(() => policy.Apply(taskFactory, retryLogger, cancellationToken), cancellationToken);
    }

    public static Task RunIsolated(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Task.Run(() => policy.Apply(taskFactory, null, cancellationToken), cancellationToken);
    }

    public static Task RunIsolated(
        this IRetryPolicy policy,
        Func<CancellationToken, Task> taskFactory,
        RetryLogger? retryLogger,
        CancellationToken cancellationToken = default)
    {
        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Task.Run(() => policy.Apply(taskFactory, retryLogger, cancellationToken), cancellationToken);
    }
}
