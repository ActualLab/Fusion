using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD103

public interface IRpcComputeMethodFunction : IComputeMethodFunction
{
    RpcMethodDef RpcMethodDef { get; }
    object? LocalTarget { get; }
}

public class RpcComputeMethodFunction<T>(
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef,
    object? localTarget,
    FusionInternalHub hub
    ) : ComputeMethodFunction<T>(methodDef, hub), IRpcComputeMethodFunction
{
    private string? _toString;

    RpcMethodDef IRpcComputeMethodFunction.RpcMethodDef => RpcMethodDef;
    object? IRpcComputeMethodFunction.LocalTarget => LocalTarget;

    public readonly RpcMethodDef RpcMethodDef = rpcMethodDef;
    public readonly RpcSafeCallRouter RpcCallRouter = rpcMethodDef.Hub.InternalServices.CallRouter;
    public readonly RpcComputeCallOptions RpcCallOptions = hub.RpcComputeCallOptions;
    public readonly IRemoteComputedCache? RemoteComputedCache = hub.RemoteComputedCache;
    public readonly object? LocalTarget = localTarget;

    public override string ToString()
        => _toString ??= ZString.Concat('*', base.ToString());

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override async ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            try {
                var peer = RpcCallRouter.Invoke(RpcMethodDef, typedInput.Invocation.Arguments);
                if (peer.ConnectionKind == RpcPeerConnectionKind.Local) {
                    // Compute local
                    var computed = new LocalRpcComputed<T>(ComputedOptions, typedInput);
                    using var _ = Computed.BeginCompute(computed);
                    if (LocalTarget != null) {
                        // With local target - it's a ClientAndServer case & the Service.Method is invoked
                        try {
                            await MethodDef.TargetAsyncInvoker
                                .Invoke(LocalTarget!, typedInput.Invocation.Arguments)
                                .ConfigureAwait(false);
                            computed.CaptureOriginal();
                            return computed;
                        }
                        catch (Exception e) when (ComputedImpl.FinalizeAndTryReturnComputed(computed, e, cancellationToken)) {
                            return computed;
                        }
                    }

                    // Without local target - it's a Hybrid case & the base.Method is invoked
                    try {
                        var result = InvokeImplementation(typedInput, cancellationToken);
                        if (typedInput.MethodDef.ReturnsValueTask) {
                            var output = await ((ValueTask<T>)result).ConfigureAwait(false);
                            computed.TrySetOutput(output);
                        }
                        else {
                            var output = await ((Task<T>)result).ConfigureAwait(false);
                            computed.TrySetOutput(output);
                        }
                        return computed;
                    }
                    catch (Exception e) {
                        var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                            nameof(Compute), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                        if (delayTask == SpecialTasks.MustThrow)
                            throw;
                        if (delayTask == SpecialTasks.MustReturn)
                            return computed;

                        await delayTask.ConfigureAwait(false);
                        continue;
                    }
                }

                // Compute remote
                try {
                    var cache = GetCache(typedInput);
                    // existing != null -> it's invalidated, so no matter what's cached, we ignore it
                    return existing == null && cache != null
                        ? await ComputeCachedOrRpc(typedInput, cache, peer, cancellationToken)
                            .ConfigureAwait(false)
                        : await ComputeRpc(typedInput, cache, (RemoteRpcComputed<T>)existing!, peer, cancellationToken)
                            .ConfigureAwait(false);
                }
                catch (Exception e) {
                    var delayTask = TryReprocessServerSideCancellation(typedInput, e, startedAt, ref tryIndex, cancellationToken);
                    if (delayTask == SpecialTasks.MustThrow)
                        throw;

                    await delayTask.ConfigureAwait(false);
                }
            }
            catch (RpcRerouteException) {
                Log.LogWarning("Rerouting: {Input}", typedInput);
                await RpcMethodDef.Hub.InternalServices.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task<Computed<T>> ComputeRpc(
        ComputeMethodInput input,
        IRemoteComputedCache? cache,
        RemoteRpcComputed<T>? existing,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        var cacheInfoCapture = cache != null ? new RpcCacheInfoCapture() : null;
        var call = await SendRpcCall(input, cacheInfoCapture, peer, cancellationToken).ConfigureAwait(false);
        if (call == null) {
            // A totally possible case: even though we provide the peer,
            // the interceptor may end up rerouting the call to a local peer
            // because the initial one becomes marked as requiring rerouting.
            throw RpcRerouteException.MustRerouteToLocal();
        }

        var result = call.ResultTask.ToResultSynchronously();
        if (result.Error is OperationCanceledException e) // Also handles RpcRerouteException
            throw e; // We treat server-side cancellations the same way as client-side cancellations

        RpcCacheEntry? cacheEntry = null;
        if (cacheInfoCapture != null && cacheInfoCapture.HasKeyAndData(out var key, out var dataSource)) {
            // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
            var dataResult = await dataSource.Task.ResultAwait(false);
            var data = dataResult.IsValue(out var vData) ? (TextOrBytes?)vData : null;
            cacheEntry = UpdateCache(cache!, key, data);
        }

        var computed = new RemoteRpcComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call);
        existing?.SynchronizedSource.TrySetResult(default);
        return computed;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public async ValueTask<Computed<T>> ComputeCachedOrRpc(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        var cacheInfoCapture = new RpcCacheInfoCapture(RpcCacheInfoCaptureMode.KeyOnly);
        // This is a fake call that only captures the cache key.
        // No actual RPC call happens here, and SendRpcCall must complete synchronously.
        var sendTask = SendRpcCall(input, cacheInfoCapture, peer, cancellationToken);
        if (!sendTask.IsCompleted)
            throw ActualLab.Internal.Errors.InternalError("SendRpcCall must complete synchronously here.");

        if (cacheInfoCapture.Key is not { } cacheKey) {
            // cacheKey wasn't captured - a weird case that normally shouldn't happen.
            // The best we can do here is to proceed assuming cache entry is missing,
            // i.e. perform RPC call & update cache.
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);
        }

        var cacheResultOpt = await cache.Get<T>(input, cacheKey, cancellationToken).ConfigureAwait(false);
        if (cacheResultOpt is not { } cacheResult) {
            // No cacheResult wasn't captured -> perform RPC call & update cache
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);
        }

        var cacheEntry = new RpcCacheEntry(cacheKey, cacheResult.Data);
        var cachedComputed = new RemoteRpcComputed<T>(
            input.MethodDef.ComputedOptions,
            input, cacheResult.Value,
            cacheEntry);

        // We suppress execution context flow here to ensure that
        // "true" computed won't be registered as a dependency -
        // which is correct, coz its cached version already became a dependency, and once
        // the true computed is created, its cached (prev.) version will be invalidated.
        //
        // And we can't use cancellationToken from here:
        // - We're completing the computation w/ cached value here
        // - But the code below starts the async task running the actual RPC call
        // - And if this task gets cancelled, the subscription to invalidation won't be set up,
        //   and thus the result may end up being stale forever.
        _ = ExecutionContextExt.Start(
            ExecutionContextExt.Default,
            () => ApplyRpcUpdate(input, cache, cachedComputed, peer));
        return cachedComputed;
    }

    public async Task ApplyRpcUpdate(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        RemoteRpcComputed<T> cachedComputed,
        RpcPeer peer)
    {
        // 1. Start the RPC call
        var cacheInfoCapture = new RpcCacheInfoCapture();
        var call = await SendRpcCall(input, cacheInfoCapture, peer, default).ConfigureAwait(false);
        if (call == null) {
            // The call has been rerouted to a local peer.
            // The best we can do is to invalidate cached computed to trigger its update.
            cachedComputed.Invalidate(true);
            return;
        }
        // peer = call.Peer; // The call could be routed to another peer

        // 2. Bind the call to cachedComputed
        if (!cachedComputed.BindToCall(call)) {
            // A weird case: cachedComputed is already invalidated (manually?).
            // This means the call is already aborted (see BindToCall logic),
            // and since we're performing a background update, we can just exit.
            return;
        }

        // 3. Await for its completion
        var result = call.ResultTask.ToResultSynchronously();
        if (result.Error is OperationCanceledException e) {
            // The call was cancelled on the server side - e.g. due to peer termination.
            // Retrying is the best we can do here; and since this call is already bound to `cachedComputed`,
            // we should invalidate the `call` rather than `cachedComputed`.
            var cancellationReprocessingOptions = cachedComputed.Options.CancellationReprocessing;
            var delay = cancellationReprocessingOptions.RetryDelays[1];
            Log.LogWarning(e,
                "ApplyRpcUpdate was cancelled on the server side for {Category}, will invalidate IComputed in {Delay}",
                input.Category, delay.ToShortString());
            await Task.Delay(delay).ConfigureAwait(false);
            call.SetInvalidated(true);
            return;
        }

        // 4. Get cache key & data
        TextOrBytes? data = null;
        if (cacheInfoCapture.HasKeyAndData(out var key, out var dataSource)) {
            // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
            var dataResult = await dataSource.Task.ResultAwait(false);
            data = dataResult.IsValue(out var vData) ? (TextOrBytes?)vData : null;
        }

        // 5. Re-entering the lock & check if cachedComputed is still consistent
        using var releaser = await InputLocks.Lock(input).ConfigureAwait(false);
        if (!cachedComputed.IsConsistent())
            return; // Since the call was bound to cachedComputed, it's properly cancelled already

        releaser.MarkLockedLocally();
        if (cachedComputed.CacheEntry is { } oldEntry && data?.DataEquals(oldEntry.Data) == true) {
            // Existing cached entry is still intact
            cachedComputed.SynchronizedSource.TrySetResult(default);
            return;
        }

        // 5. Now, let's update cache entry
        var cacheEntry = UpdateCache(cache, key, data);

        // 6. Create the new computed - it invalidates the cached one upon registering
        var computed = new RemoteRpcComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call);
        computed.RenewTimeouts(true);
        cachedComputed.SynchronizedSource.TrySetResult(default);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override async ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        // Double-check locking
        var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (ComputedImpl.TryUseExisting(computed, context))
            return computed!;

        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (ComputedImpl.TryUseExistingFromLock(computed, context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed;
    }

    // Protected methods

    protected internal override async Task<T> TryRecompute(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        if (RpcCallOptions.ValidateRpcCallOrigin && RpcInboundContext.Current is { Peer: { } peer }) {
            var handshake = peer.ConnectionState.Value.Handshake;
            if (handshake != null && handshake.RemoteHubId == RpcMethodDef.Hub.Id)
                throw Errors.RpcComputeMethodCallFromTheSameService(RpcMethodDef, peer.Ref);
        }

        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var existing = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (ComputedImpl.TryUseExistingFromLock(existing, context))
            return ComputedImpl.Strip(existing, context);

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed.Value;
    }

    protected static async ValueTask<RpcOutboundComputeCall<T>?> SendRpcCall(
        ComputeMethodInput input,
        RpcCacheInfoCapture? cacheInfoCapture,
        RpcPeer? peer,
        CancellationToken cancellationToken)
    {
        var context = new RpcOutboundContext(RpcComputeCallType.Id) {
            CacheInfoCapture = cacheInfoCapture,
            Peer = peer,
        };
        var invocation = input.Invocation;
        var proxy = (IProxy)invocation.Proxy;
        var rpcComputeServiceInterceptor = (RpcComputeServiceInterceptor)proxy.Interceptor;
        var computeCallRpcInterceptor = rpcComputeServiceInterceptor.ComputeCallRpcInterceptor;

        var ctIndex = input.MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0 && invocation.Arguments.GetCancellationToken(ctIndex) != cancellationToken) {
            // Fixing invocation: set CancellationToken + Context
            var arguments = invocation.Arguments with { }; // Cloning
            arguments.SetCancellationToken(ctIndex, cancellationToken);
            invocation = invocation.With(arguments, context);
        }
        else {
            // Nothing to fix: it's the same cancellation token or there is no token
            invocation = invocation.With(context);
        }
        await input.MethodDef.InterceptorAsyncInvoker.Invoke(computeCallRpcInterceptor, invocation).SilentAwait();
        return (RpcOutboundComputeCall<T>?)context.Call;
    }

    protected static RpcCacheEntry? UpdateCache(
        IRemoteComputedCache cache,
        RpcCacheKey? key,
        TextOrBytes? data)
    {
        if (key == null)
            return null;

        if (!data.HasValue) {
            cache.Remove(key); // Error -> wipe cache entry
            return null;
        }

        cache.Set(key, data.GetValueOrDefault());
        return new RpcCacheEntry(key, data.GetValueOrDefault());
    }

    protected IRemoteComputedCache? GetCache(ComputeMethodInput input)
        => input.MethodDef.ComputedOptions.RemoteComputedCacheMode == RemoteComputedCacheMode.Cache
            ? RemoteComputedCache
            : null;

    protected Task TryReprocessServerSideCancellation(ComputeMethodInput input,
        Exception error,
        CpuTimestamp startedAt,
        ref int tryIndex,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || error is not OperationCanceledException || error is RpcRerouteException)
            return SpecialTasks.MustThrow;

        // If we're here, the cancellation is triggered on the server side / due to connectivity issue

        var cancellationReprocessingOptions = input.MethodDef.ComputedOptions.CancellationReprocessing;
        if (++tryIndex > cancellationReprocessingOptions.MaxTryCount)
            return SpecialTasks.MustThrow;
        if (startedAt.Elapsed > cancellationReprocessingOptions.MaxDuration)
            return SpecialTasks.MustThrow;

        var delay = cancellationReprocessingOptions.RetryDelays[tryIndex];
        Log.LogWarning(error,
            "{Method} #{TryIndex} was cancelled on the server side for {Category}, will retry in {Delay}",
            nameof(ComputeRpc), tryIndex, input.Category, delay.ToShortString());
        return Task.Delay(delay, cancellationToken);
    }
}
