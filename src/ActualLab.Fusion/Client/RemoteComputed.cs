using ActualLab.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client;

#pragma warning disable VSTHRD104, MA0055

public interface IRemoteComputed : IComputed, IMaybeCachedValue, IDisposable
{
    public AsyncTaskMethodBuilder<RpcOutboundComputeCall?> CallSource { get; }
    public AsyncTaskMethodBuilder SynchronizedSource { get; }
    public Task WhenCallBound { get; }
    public RpcCacheEntry? CacheEntry { get; }

    public bool BindToCall(RpcOutboundComputeCall? call);
}

public class RemoteComputed<T> : ComputeMethodComputed<T>, IRemoteComputed
{
    public AsyncTaskMethodBuilder<RpcOutboundComputeCall?> CallSource { get; }
    public AsyncTaskMethodBuilder SynchronizedSource { get; }

    Task IRemoteComputed.WhenCallBound => CallSource.Task;
    public Task<RpcOutboundComputeCall?> WhenCallBound => CallSource.Task;
    public RpcCacheEntry? CacheEntry { get; }
    public Task WhenSynchronized => SynchronizedSource.Task;

    // Called when computed is populated from cache
    public RemoteComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        RpcCacheEntry? cacheEntry)
        : base(options, input, output, true, SkipComputedRegistration.Option)
    {
        CallSource = AsyncTaskMethodBuilderExt.New<RpcOutboundComputeCall?>();
        CacheEntry = cacheEntry;
        SynchronizedSource = AsyncTaskMethodBuilderExt.New();
        ComputedRegistry.Instance.Register(this);
        StartAutoInvalidation();
    }

    // Called when computed is populated after RPC call
    public RemoteComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        RpcCacheEntry? cacheEntry,
        RpcOutboundComputeCall call)
        : base(options, input, output, true, SkipComputedRegistration.Option)
    {
        CallSource = AsyncTaskMethodBuilderExt.New<RpcOutboundComputeCall?>().WithResult(call);
        CacheEntry = cacheEntry;
        SynchronizedSource = AlwaysSynchronized.Source;
        ComputedRegistry.Instance.Register(this);

        // This should go after .Register(this)
        BindWhenInvalidatedToCall(call);
        StartAutoInvalidation();
    }

    ~RemoteComputed()
        => Dispose();

    public void Dispose()
    {
        if (!WhenCallBound.IsCompleted)
            return;

        var call = WhenCallBound.Result;
        call?.CompleteAndUnregister(notifyCancelled: !this.IsInvalidated());
    }

    public bool BindToCall(RpcOutboundComputeCall? call)
    {
        if (!CallSource.TrySetResult(call)) {
            // Another call is already bound
            if (call == null) {
                // Call from OnInvalidated - we need to cancel the old call
                var boundCall = WhenCallBound.Result;
                boundCall?.SetInvalidated(true);
            }
            else {
                // Normal BindToCall, we cancel the call to ensure its invalidation sub. is gone
                call.SetInvalidated(true);
            }
            return false;
        }
        // If we're here, the computed is bound to the specified call

        if (call != null) // Otherwise the null call originates from OnInvalidated
            BindWhenInvalidatedToCall(call);
        return true;
    }

    public void BindWhenInvalidatedToCall(RpcOutboundComputeCall call)
    {
        var whenInvalidated = call.WhenInvalidated;
        if (whenInvalidated.IsCompleted) {
            // No call (call prepare error - e.g. if there is no such RPC service),
            // or the call result is already invalidated
            Invalidate(true);
            return;
        }

        _ = whenInvalidated.ContinueWith(
            _ => Invalidate(true),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    // Protected methods

    protected internal override void InvalidateFromCall()
    {
        // If such a computed is invalidated from Invalidation.Begin() block,
        // more likely than not it's a mistake - i.e. they're just replicas of
        // computed instances living on another host, so their original instances
        // have to be invalidated rather than the replicas.
        //
        // That's why the best we can do here is to just ignore the call.
    }

    protected override void OnInvalidated()
    {
        BindToCall(null);

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
