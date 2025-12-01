using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IComputeFunction : IHasServices
{
    public FusionHub Hub { get; }
    public Type OutputType { get; }

    public Task<Computed> ProduceComputed(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default);
}

public abstract class ComputeFunction(FusionHub hub, Type outputType) : IComputeFunction
{
    protected static AsyncLockSet<ComputedInput> InputLocks {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComputedRegistry.InputLocks;
    }

    private LazySlim<ILogger?>? _debugLog;

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
    public virtual async Task<Computed> ProduceComputed(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);
        try {
            var computed = ComputedRegistry.Get(input); // = input.GetExistingComputed()
            if (ComputedImpl.TryUseExistingFromLock(computed, context))
                return computed!;

            if (input.IsDisposed) {
                // We're going to await for an indefinitely long task here, and there is a chance
                // this task is going to be GC-collected. So we need to release the async lock here
                // to prevent a memory leak in AsyncLocks set, which is going to keep our
                // never-released lock otherwise.
                releaser.Dispose();
                releaser = default;
                // Compute takes indefinitely long for disposed compute service's methods
                await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
            }

            releaser.MarkLockedLocally(unmarkOnRelease: false);
            computed = await ProduceComputedImpl(input, computed, cancellationToken).ConfigureAwait(false);
            ComputedImpl.UseNew(computed, context);
            return computed;
        }
        finally {
            releaser.Dispose();
        }
    }

    // Protected & private

    protected internal abstract ValueTask<Computed> ProduceComputedImpl(
        ComputedInput input, Computed? existing, CancellationToken cancellationToken);
}
