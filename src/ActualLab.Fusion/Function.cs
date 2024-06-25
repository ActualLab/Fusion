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

        var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);
        try {
            computed = GetExisting(input);
            if (computed.TryUseExistingFromLock(context))
                return computed!;

            if (input.IsDisposed) {
                // We're going to await for indefinitely long task here, and there is a chance
                // this task is going to be GC-collected. So we need to release the async lock here
                // to prevent a memory leak in AsyncLocks set, which is going to keep our
                // never-released lock otherwise.
                releaser.Dispose();
                releaser = default;
                // Compute takes indefinitely long for disposed compute service's methods
                await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            releaser.MarkLockedLocally();
            computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
            computed.UseNew(context);
            return computed;
        }
        finally {
            releaser.Dispose();
        }
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
        var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);
        try {
            var computed = GetExisting(input);
            if (computed.TryUseExistingFromLock(context))
                return computed.Strip(context);

            if (input.IsDisposed) {
                // We're going to await for indefinitely long task here, and there is a chance
                // this task is going to be GC-collected. So we need to release the async lock here
                // to prevent a memory leak in AsyncLocks set, which is going to keep our
                // never-released lock otherwise.
                releaser.Dispose();
                releaser = default;
                // Compute takes indefinitely long for disposed compute service's methods
                await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            releaser.MarkLockedLocally();
            computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
            computed.UseNew(context);
            return computed.Value;
        }
        finally {
            releaser.Dispose();
        }

    }

    protected Computed<T>? GetExisting(ComputedInput input)
        => input.GetExistingComputed() as Computed<T>;

    // Protected & private

    protected abstract ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing, CancellationToken cancellationToken);
}
