using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc.Caching;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD104
#pragma warning disable MA0055

public interface IClientComputed : IComputed, IMaybeCachedValue, IDisposable
{
    Task WhenCallBound { get; }
    RpcCacheEntry? CacheEntry { get; }
}

public class ClientComputed<T> : ComputeMethodComputed<T>, IClientComputed
{
    internal readonly TaskCompletionSource<RpcOutboundComputeCall<T>?> CallSource;
    internal readonly TaskCompletionSource<Unit> SynchronizedSource;

    Task IClientComputed.WhenCallBound => CallSource.Task;
    public Task<RpcOutboundComputeCall<T>?> WhenCallBound => CallSource.Task;
    public RpcCacheEntry? CacheEntry { get; }
    public Task WhenSynchronized => SynchronizedSource.Task;

    public ClientComputed(
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

    public ClientComputed(
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
    ~ClientComputed()
        => Dispose();

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    public void Dispose()
#pragma warning restore IL2046
    {
        if (!WhenCallBound.IsCompleted)
            return;

        var call = WhenCallBound.Result;
        call?.Unregister(!this.IsInvalidated());
    }

    // Internal methods

    internal bool BindToCall(RpcOutboundComputeCall<T>? call)
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

    protected void BindWhenInvalidatedToCall(RpcOutboundComputeCall<T> call)
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
    protected override void OnInvalidated()
    {
        BindToCall(null);
        // PseudoUnregister is used here just to trigger the
        // Unregistered event in ComputedRegistry.
        // We want to keep this computed unless SynchronizedSource is
        // AlwaysSynchronized.Source, which means it doesn't use cache.
        // Otherwise (i.e. when SynchronizedSource is actually used)
        // the next computed won't reuse the existing SynchronizedSource,
        // which may render it as indefinitely incomplete.
        if (ReferenceEquals(SynchronizedSource, AlwaysSynchronized.Source))
            ComputedRegistry.Instance.Unregister(this);
        else
            ComputedRegistry.Instance.PseudoUnregister(this);
        CancelTimeouts();
    }
}
