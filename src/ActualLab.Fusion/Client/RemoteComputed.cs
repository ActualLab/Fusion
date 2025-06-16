using ActualLab.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client;

#pragma warning disable VSTHRD104, MA0055

public interface IRemoteComputed : IComputed, IDisposable
{
    public AsyncTaskMethodBuilder<RpcOutboundComputeCall?> CallSource { get; }
    public AsyncTaskMethodBuilder SynchronizedSource { get; }
    public Task<RpcOutboundComputeCall?> WhenCallBound { get; }
    public Task WhenSynchronized { get; }
    public RpcCacheEntry? CacheEntry { get; }
}

public class RemoteComputed<T> : ComputeMethodComputed<T>, IRemoteComputed
{
    public AsyncTaskMethodBuilder<RpcOutboundComputeCall?> CallSource { get; }
    public AsyncTaskMethodBuilder SynchronizedSource { get; }

    public Task<RpcOutboundComputeCall?> WhenCallBound => CallSource.Task;
    public RpcCacheEntry? CacheEntry { get; }
    public Task WhenSynchronized => SynchronizedSource.Task;

    // Called when computed is populated after RPC call
    public RemoteComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result output,
        RpcCacheEntry? cacheEntry,
        RpcOutboundComputeCall? call = null)
        : base(options, input, output, true, SkipComputedRegistration.Option)
    {
        CallSource = AsyncTaskMethodBuilderExt.New<RpcOutboundComputeCall?>();
        if (call != null) {
            CallSource.SetResult(call);
            SynchronizedSource = AlwaysSynchronized.Source;
        }
        else
            SynchronizedSource = AsyncTaskMethodBuilderExt.New();
        CacheEntry = cacheEntry;
        ComputedRegistry.Instance.Register(this);

        // This should go after .Register(this)
        if (call != null)
            this.BindWhenInvalidatedToCall(call);
        StartAutoInvalidation();
    }

    ~RemoteComputed()
        => Dispose();

    public void Dispose()
    {
        if (!WhenCallBound.IsCompleted)
            return;

        var call = WhenCallBound.GetAwaiter().GetResult();
        call?.CompleteAndUnregister(notifyCancelled: !this.IsInvalidated());
    }

    // Protected methods

    protected override void OnInvalidated()
    {
        this.BindToCall(null);

        // PseudoUnregister triggers the Unregistered event in ComputedRegistry w/o actual unregistration.
        // We have to keep this computed in the registry even after the invalidation,
        // coz otherwise:
        // - Its SynchronizedSource could be "lost" w/o ever transitioning to Completed state.
        //   So we _at least_ must keep it in the registry while its SynchronizedSource isn't completed.
        // - The logic in RemoteComputeMethodFunction.Compute will resort to cache lookup
        //   & produce another unsynchronized computed shortly after.
        //   And we want to avoid an extra cache lookup - even if it's at cost of some extra
        //   RAM consumption.
        if (Options.RemoteComputedCacheMode == RemoteComputedCacheMode.Cache) // && !SynchronizedSource.Task.IsCompleted)
            ComputedRegistry.Instance.PseudoUnregister(this);
        else
            ComputedRegistry.Instance.Unregister(this);
        CancelTimeouts();
    }
}
