namespace ActualLab.Resilience;

public static class RetryPolicyExt
{
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
        Action<Exception, int>? errorLogger = null,
        CancellationToken cancellationToken = default)
    {
        return policy.Apply(UnitTaskFactory, errorLogger, cancellationToken);

        async Task<Unit> UnitTaskFactory(CancellationToken cancellationToken1)
        {
            await taskFactory.Invoke(cancellationToken1).ConfigureAwait(false);
            return default;
        }
    }
}
