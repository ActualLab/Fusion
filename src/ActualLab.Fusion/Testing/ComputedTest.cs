using System.Runtime.ExceptionServices;

namespace ActualLab.Fusion.Testing;

public static class ComputedTest
{
    private static LazySlim<IServiceProvider> _defaultServices = new(
        () => new ServiceCollection().AddFusion().Services.BuildServiceProvider());

    public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public static IServiceProvider DefaultServices {
        get => _defaultServices.Value;
        set => _defaultServices = new LazySlim<IServiceProvider>(value);
    }

    public static Task When(Func<CancellationToken, Task> assertion, TimeSpan? timeout = null)
        => When(DefaultServices, assertion, timeout);

    public static Task<T> When<T>(Func<CancellationToken, Task<T>> assertion, TimeSpan? timeout = null)
        => When(DefaultServices, assertion, timeout);

    public static async Task When(
        IServiceProvider services,
        Func<CancellationToken, Task> assertion,
        TimeSpan? timeout = null)
    {
        var lastError = (ExceptionDispatchInfo?)null;
        var computedSource = new AnonymousComputedSource<bool>(services,
            async (_, ct) => {
                try {
                    await assertion.Invoke(ct).ConfigureAwait(false);
                    lastError = null;
                    return true;
                }
                catch (Exception e) when (!e.IsCancellationOf(ct)) {
                    lastError = ExceptionDispatchInfo.Capture(e);
                    return false;
                }
            });
        var vTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(vTimeout);
        try {
            await computedSource.When(x => x, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsCancellationOf(timeoutCts.Token)) {
            lastError?.Throw();
            throw new TimeoutException($"{nameof(ComputedTest)}.{nameof(When)} timed out ({vTimeout.ToShortString()}).");
        }
    }

    public static async Task<T> When<T>(
        IServiceProvider services,
        Func<CancellationToken, Task<T>> assertion,
        TimeSpan? timeout = null)
    {
        var lastResult = default(T);
        var lastError = (ExceptionDispatchInfo?)null;
        var computedSource = new AnonymousComputedSource<bool>(services,
            async (_, ct) => {
                try {
                    lastResult = await assertion.Invoke(ct).ConfigureAwait(false);
                    lastError = null;
                    return true;
                }
                catch (Exception e) when (!e.IsCancellationOf(ct)) {
                    lastResult = default;
                    lastError = ExceptionDispatchInfo.Capture(e);
                    return false;
                }
            });
        var vTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(vTimeout);
        try {
            await computedSource.When(x => x, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsCancellationOf(timeoutCts.Token)) {
            lastError?.Throw();
            throw new TimeoutException($"{nameof(ComputedTest)}.{nameof(When)} timed out ({vTimeout.ToShortString()}).");
        }
        return lastResult!;
    }
}
