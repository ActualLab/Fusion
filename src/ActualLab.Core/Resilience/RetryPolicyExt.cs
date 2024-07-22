namespace ActualLab.Resilience;

public static class RetryPolicyExt
{
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
