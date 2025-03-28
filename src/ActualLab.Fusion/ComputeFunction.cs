using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IComputeFunction : IHasServices
{
    public FusionHub Hub { get; }
    public Type OutputType { get; }
}

public interface IComputeFunction<T> : IComputeFunction
{
    public ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default);
    public Task<T> InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default);
}

public abstract class ComputeFunctionBase<T>(FusionHub hub, Type outputType) : IComputeFunction<T>
{
    protected static AsyncLockSet<ComputedInput> InputLocks {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComputedRegistry.Instance.InputLocks;
    }

    private LazySlim<ILogger?>? _debugLog;

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());
    protected ILogger? DebugLog => (_debugLog ??= LazySlim.New(Log.IfEnabled(LogLevel.Debug))).Value;

    public readonly FusionHub Hub = hub;
    public readonly IServiceProvider Services = hub.Services;
    public readonly Type OutputType = outputType;

    // IComputeFunction implementation
    IServiceProvider IHasServices.Services => Services;
    FusionHub IComputeFunction.Hub => Hub;
    Type IComputeFunction.OutputType => OutputType;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual async ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        // Double-check locking
        var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (ComputedImpl.TryUseExisting(computed, context))
            return computed!;

        var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);
        try {
            computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
            if (ComputedImpl.TryUseExistingFromLock(computed, context))
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
            ComputedImpl.UseNew(computed, context);
            return computed;
        }
        finally {
            releaser.Dispose();
        }
    }

    public Task<T> InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        return ComputedImpl.TryUseExisting(computed, context)
            ? ComputedImpl.StripToTask(computed, context)
            : TryRecompute(input, context, cancellationToken);
    }

    protected internal virtual async Task<T> TryRecompute(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);
        try {
            var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
            if (ComputedImpl.TryUseExistingFromLock(computed, context))
                return ComputedImpl.Strip(computed, context);

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
            ComputedImpl.UseNew(computed, context);
            return computed.Value;
        }
        finally {
            releaser.Dispose();
        }
    }

    // Protected & private

    protected abstract ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing, CancellationToken cancellationToken);
}
