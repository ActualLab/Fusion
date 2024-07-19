using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc.Caching;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD104, MA0055

public interface IRemoteRpcComputed : IComputed, IMaybeCachedValue, IDisposable
{
    Task WhenCallBound { get; }
    RpcCacheEntry? CacheEntry { get; }
}

public class RemoteRpcComputed<T> : ComputeMethodComputed<T>, IRemoteRpcComputed
{
    internal readonly TaskCompletionSource<RpcOutboundComputeCall<T>?> CallSource;
    internal readonly TaskCompletionSource<Unit> SynchronizedSource;

    Task IRemoteRpcComputed.WhenCallBound => CallSource.Task;
    public Task<RpcOutboundComputeCall<T>?> WhenCallBound => CallSource.Task;
    public RpcCacheEntry? CacheEntry { get; }
    public Task WhenSynchronized => SynchronizedSource.Task;

    public RemoteRpcComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        RpcCacheEntry? cacheEntry,
        TaskCompletionSource<Unit>? synchronizedSource = null)
        : base(options, input, output, true, SkipComputedRegistration.Option)
    {
        CallSource = TaskCompletionSourceExt.New<RpcOutboundComputeCall<T>?>();
        CacheEntry = cacheEntry;
        SynchronizedSource = synchronizedSource ?? TaskCompletionSourceExt.New<Unit>();
        ComputedRegistry.Instance.Register(this);
        StartAutoInvalidation();
    }

    public RemoteRpcComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        RpcCacheEntry? cacheEntry,
        RpcOutboundComputeCall<T> call,
        TaskCompletionSource<Unit>? synchronizedSource = null)
        : base(options, input, output, true, SkipComputedRegistration.Option)
    {
        CallSource = TaskCompletionSourceExt.New<RpcOutboundComputeCall<T>?>().WithResult(call);
        CacheEntry = cacheEntry;
        SynchronizedSource = synchronizedSource ??= TaskCompletionSourceExt.New<Unit>();
        ComputedRegistry.Instance.Register(this);

        // This should go after .Register(this)
        synchronizedSource.TrySetResult(default); // Call is there -> synchronized
        BindWhenInvalidatedToCall(call);
        StartAutoInvalidation();
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    ~RemoteRpcComputed()
        => Dispose();

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    public void Dispose()
#pragma warning restore IL2046
    {
        if (!WhenCallBound.IsCompleted)
            return;

        var call = WhenCallBound.Result;
        call?.CompleteAndUnregister(notifyCancelled: !this.IsInvalidated());
    }

    public bool BindToCall(RpcOutboundComputeCall<T>? call)
    {
#pragma warning disable IL2026
        if (!CallSource.TrySetResult(call)) {
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
        if (call == null) // Invalidated before being bound to call - nothing else to do
            return true;
#pragma warning restore IL2026

        BindWhenInvalidatedToCall(call);
        return true;
    }

    public void BindWhenInvalidatedToCall(RpcOutboundComputeCall<T> call)
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
        // if its SynchronizedSource is incomplete,
        // otherwise this SynchronizedSource won't be reused in the next computed,
        // and thus it may stay incomplete indefinitely.
        if (SynchronizedSource.Task.IsCompleted)
            ComputedRegistry.Instance.Unregister(this);
        else
            ComputedRegistry.Instance.PseudoUnregister(this);
        CancelTimeouts();
    }
}
