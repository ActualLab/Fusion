using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IFunction : IHasServices;

public interface IFunction<T> : IFunction
{
    ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default);
    Task<T> InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default);
}

public abstract class FunctionBase<T>(IServiceProvider services) : IFunction<T>
{
    protected static AsyncLockSet<ComputedInput> InputLocks => ComputedRegistry.Instance.InputLocks;

    private ILogger? _log;
    private LazySlim<ILogger?>? _debugLog;

    protected ILogger Log => _log ??= Services.LogFor(GetType());
    protected ILogger? DebugLog => (_debugLog ??= LazySlim.New(Log.IfEnabled(LogLevel.Debug))).Value;

    public IServiceProvider Services { get; } = services;

    public virtual async ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        // Double-check locking
        var computed = GetExisting(input);
        if (computed.TryUseExisting(context))
            return computed!;

        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        computed = GetExisting(input);
        if (computed.TryUseExistingFromLock(context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed;
    }

    public virtual Task<T> InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var computed = GetExisting(input);
        return computed.TryUseExisting(context)
            ? computed.StripToTask(context)
            : TryRecompute(input, context, cancellationToken);
    }

    protected async Task<T> TryRecompute(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var computed = GetExisting(input);
        if (computed.TryUseExistingFromLock(context))
            return computed.Strip(context);

        releaser.MarkLockedLocally();
        computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed.Value;
    }

    protected Computed<T>? GetExisting(ComputedInput input)
        => input.GetExistingComputed() as Computed<T>;

    // Protected & private

    protected abstract ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing, CancellationToken cancellationToken);
}
