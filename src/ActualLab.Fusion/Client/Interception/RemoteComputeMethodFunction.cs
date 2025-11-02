using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Interception.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD103

public sealed class RemoteComputeMethodFunction<T>(
    FusionHub hub,
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef
    ) : RemoteComputeMethodFunction(hub, methodDef, rpcMethodDef)
{
    protected override Computed NewComputed(ComputeMethodInput input)
        => throw ActualLab.Internal.Errors.InternalError($"This method should never be called in {GetType().GetName()}.");

    protected override Computed NewReplicaComputed(ComputeMethodInput input)
        => new ReplicaComputed<T>(ComputedOptions, input);

    protected override Computed NewRemoteComputed(ComputedOptions options, ComputeMethodInput input, Result output, RpcCacheEntry? cacheEntry, RpcOutboundComputeCall? call = null)
        => new RemoteComputed<T>(options, input, output, cacheEntry, call);
}

public abstract class RemoteComputeMethodFunction(
    FusionHub hub,
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef
    ) : ComputeMethodFunction(hub, methodDef)
{
    private string? _toString;

    protected readonly (LogLevel LogLevel, int MaxDataLength) LogCacheEntryUpdateSettings
        = hub.RemoteComputeServiceInterceptorOptions.LogCacheEntryUpdateSettings;
    protected ILogger CacheLog => Hub.RemoteComputedCacheLog;

    public readonly RpcHub RpcHub = hub.RpcHub;
    public readonly RpcMethodDef RpcMethodDef = rpcMethodDef;
    public readonly RpcSafeCallRouter RpcSafeCallRouter = hub.RpcHub.InternalServices.SafeCallRouter;
    public readonly IRemoteComputedCache? RemoteComputedCache = hub.RemoteComputedCache;

    public override string ToString()
        => _toString ??= "*" + base.ToString();

    public object? RemoteComputeServiceInterceptorHandler(Invocation invocation)
    {
        var input = new ComputeMethodInput(this, MethodDef, invocation);
        var cancellationToken = invocation.Arguments.GetCancellationToken(CancellationTokenIndex); // Auto-handles -1 index
        try {
            var task = input.GetOrProduceValuePromise(ComputeContext.Current, ComputedSynchronizer.Current, cancellationToken);
            return MethodDef.WrapAsyncInvokerResultOfAsyncMethodUntyped(task);
        }
        finally {
            if (cancellationToken.CanBeCanceled)
                // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                invocation.Arguments.SetCancellationToken(CancellationTokenIndex, default);
        }
    }

    protected internal override async ValueTask<Computed> ProduceComputedImpl(
        ComputedInput input, Computed? existing, CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            try {
                // We don't use RpcCallOptions here, because this method is typically called from
                // a post-async-lock block, so the original RpcCallOptions.Peer won't be available
                // at this point. And that's why there is also no need to reset it.
                var peer = RpcSafeCallRouter.Invoke(RpcMethodDef, typedInput.Invocation.Arguments);
                peer.ThrowIfRerouted();

                if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                    // Local computation / no RPC call scenario
                    // Proxy is either:
                    // - a pure client (interface proxy), so InvokeIntercepted will fail for it
                    //   (there is no base.Method)
                    // - or a Distributed mode service, so its base.Method should be invoked
                    var computed = NewReplicaComputed(typedInput);
                    using var _ = Computed.BeginCompute(computed);
                    try {
                        ValueTask<object?> invokeInterceptedUntypedTask;
                        // Force distributed service to route to local
                        using (RpcCallOptions.Activate(peer)) {
                            // No "await" inside this block!
                            invokeInterceptedUntypedTask = typedInput.InvokeInterceptedUntyped(cancellationToken);
                        }
                        var result = await invokeInterceptedUntypedTask.ConfigureAwait(false);
                        computed.TrySetValue(result);
                        return computed;
                    }
                    catch (Exception e) {
                        var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                            nameof(ProduceComputedImpl), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                        if (delayTask == SpecialTasks.MustThrow)
                            throw;
                        if (delayTask == SpecialTasks.MustReturn)
                            return computed;

                        await delayTask.ConfigureAwait(false);
                        continue;
                    }
                }

                // RPC call scenario
                try {
                    var cache = GetCache(typedInput);
                    // existing is not null -> it's invalidated, and since the cached value is even more outdated,
                    // it doesn't make sense to fetch it
                    return existing is null && cache is not null
                        ? await ComputeCachedOrRpc(typedInput, cache, peer, cancellationToken)
                            .ConfigureAwait(false)
                        : await ComputeRpc(typedInput, cache, existing, peer, cancellationToken)
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
                await RpcHub.InternalServices.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<Computed> ComputeRpc(
        ComputeMethodInput input,
        IRemoteComputedCache? cache,
        Computed? existing,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        // SendRpcCall uses an interceptor with AssumeConnected == false,
        // so we await for the connection here.
        var whenConnected = WhenConnectedChecked(input, peer, cancellationToken);
        if (!whenConnected.IsCompletedSuccessfully()) // Slow path
            await whenConnected.ConfigureAwait(false);

        var existingRemoteComputed = existing as IRemoteComputed;
        var existingCacheEntry = existingRemoteComputed?.CacheEntry;
        var cacheInfoCapture = cache is not null
            ? new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash)
            : null;
        var (result, call) = await SendRpcCall(input, peer, cacheInfoCapture, cancellationToken).ConfigureAwait(false);
        var (value, error) = result;
        if (error is OperationCanceledException e) // Also handles RpcRerouteException
            throw e; // We treat server-side cancellations the same way as client-side cancellations

        RpcCacheEntry? cacheEntry = null;
        if (cacheInfoCapture is not null && cacheInfoCapture.HasKeyAndValue(out var cacheKey, out var cacheValueOrError)) {
            // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
            var cacheValue = cacheValueOrError as RpcCacheValue;

            if (existingCacheEntry is null)
                cacheEntry = UpdateCache(cache!, cacheKey, cacheValue, value);
            else {
                if (cacheValue is not null && cacheValue.HashOrDataEquals(existingCacheEntry.Value))
                    cacheEntry = existingCacheEntry; // The existing cached entry is still intact
                else
                    cacheEntry = UpdateCache(cache!, cacheKey, cacheValue, value, existing);
            }
        }

        var computed = NewRemoteComputed(input.MethodDef.ComputedOptions, input, result, cacheEntry, call);
        existingRemoteComputed?.SynchronizedSource.TrySetResult();
        return computed;
    }

    public async ValueTask<Computed> ComputeCachedOrRpc(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        var cacheInfoCapture = new RpcCacheInfoCapture(RpcCacheInfoCaptureMode.KeyOnly);
        // This is a fake call that only captures the cache key.
        // No actual RPC call happens here, and SendRpcCall completes synchronously here.
        var sendTask = SendRpcCall(input, peer, cacheInfoCapture, cancellationToken);
        if (!sendTask.IsCompleted)
            throw ActualLab.Internal.Errors.InternalError($"{nameof(SendRpcCall)} must complete synchronously here.");

        if (cacheInfoCapture.Key is not { } cacheKey) {
            // cacheKey wasn't captured - a weird case that normally shouldn't happen.
            // The best we can do here is to proceed assuming that the cache entry is missing,
            // i.e., perform an RPC call and update the cache.
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);
        }

        var cacheEntry = await cache.Get(input, cacheKey, cancellationToken).ConfigureAwait(false);
        if (cacheEntry is null)
            // No cacheEntry was captured -> perform RPC call and update cache
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);

        var cachedComputed = NewRemoteComputed(
            input.MethodDef.ComputedOptions, input, Result.NewUntyped(cacheEntry.DeserializedValue), cacheEntry);

        // We suppress execution context flow here to ensure that
        // "true" computed won't be registered as a dependency -
        // which is correct, coz its cached version already became a dependency, and once
        // the true computed is created, its cached (prev.) version will be invalidated.
        //
        // And we can't use cancellationToken from here:
        // - We're completing the computation w/ cached value here
        // - But the code below starts the async task running the actual RPC call
        // - And if this task gets canceled, the subscription to invalidation won't be set up,
        //   and thus the result may end up being stale forever.
        _ = ExecutionContextExt.Start(
            ExecutionContextExt.Default,
            () => ApplyRpcUpdate(input, cache, cachedComputed, peer));
        return cachedComputed;
    }

    public async Task ApplyRpcUpdate(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        Computed cachedComputed,
        RpcPeer peer)
    {
        // 0. Await for RPC call delay
        var delayTask = Caching.RemoteComputedCache.HitToCallDelayer?.Invoke(input, peer);
        if (delayTask is { IsCompleted: false })
            await delayTask.SilentAwait(false);

        // 1. Await for the connection
        // SendRpcCall uses an interceptor with AssumeConnected == false, so we have to do it here.
        var whenConnected = WhenConnectedChecked(input, peer);
        if (!whenConnected.IsCompletedSuccessfully()) { // Slow path
            try {
                await whenConnected.ConfigureAwait(false);
            }
            catch (Exception whenConnectedError) {
                const string reason =
                    $"{nameof(RemoteComputeMethodFunction)}.{nameof(ApplyRpcUpdate)}: {nameof(WhenConnectedChecked)} failed";
                await InvalidateOnError(cachedComputed, whenConnectedError, reason).ConfigureAwait(false);
                return;
            }
        }

        // 2. Send the RPC call
        var remoteCachedComputed = (IRemoteComputed)cachedComputed;
        var existingCacheEntry = remoteCachedComputed.CacheEntry;
        var cacheInfoCapture = new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash);
        var (result, call) = await SendRpcCall(input, peer, cacheInfoCapture, default).ConfigureAwait(false);
        var (value, error) = result;
        if (call is null) {
            const string reason =
                $"{nameof(RemoteComputeMethodFunction)}.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} returned null call";
            await InvalidateToReroute(cachedComputed, result.Error, reason).ConfigureAwait(false);
            return;
        }
        if (error is RpcRerouteException) {
            const string reason =
                $"{nameof(RemoteComputeMethodFunction)}.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} threw {nameof(RpcRerouteException)}";
            await InvalidateToReroute(cachedComputed, result.Error, reason).ConfigureAwait(false);
            return;
        }

        // 3. Bind the call to cachedComputed
        if (!remoteCachedComputed.BindToCall(call)) {
            // A weird case: cachedComputed is already invalidated (manually?).
            // This means the call is already aborted (see BindToCall logic),
            // and since we're performing a background update, we can just exit.
            return;
        }

        // 4. Handle OperationCanceledException
        if (error is OperationCanceledException e) {
            // The call was cancelled on the server side - e.g. due to peer termination.
            // Retrying is the best we can do here; and since this call is already bound to `cachedComputed`,
            // we should invalidate the `call` rather than `cachedComputed`.
            var cancellationReprocessingOptions = cachedComputed.Options.CancellationReprocessing;
            var delay = cancellationReprocessingOptions.RetryDelays[1];
            Log.LogWarning(e,
                "ApplyRpcUpdate was cancelled on the server side for {Category}, will invalidate IComputed in {Delay}",
                input.Category, delay.ToShortString());
            await Task.Delay(delay).ConfigureAwait(false);
            const string reason =
                $"{nameof(RemoteComputeMethodFunction)}.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} returned a result with OperationCanceledException";
            call.SetInvalidated(true, reason);
            return;
        }

        // 5. Get cached key and data
        cacheInfoCapture.RequireKeyAndValue(out var cacheKey, out var cacheValueOrError);
        var cacheValue = cacheValueOrError as RpcCacheValue;

        // 6. Re-entering the lock and check if cachedComputed is still consistent
        using var releaser = await InputLocks.Lock(input).ConfigureAwait(false);
        if (!cachedComputed.IsConsistent())
            return; // Since the call was bound to cachedComputed, it's properly cancelled already

        releaser.MarkLockedLocally(unmarkOnRelease: false);

        // 7. Update cache
        RpcCacheEntry? cacheEntry;
        if (existingCacheEntry is null)
            cacheEntry = UpdateCache(cache, cacheKey, cacheValue, value);
        else {
            if (cacheValue is not null && cacheValue.HashOrDataEquals(existingCacheEntry.Value)) {
                // The existing cached entry is still intact
                remoteCachedComputed.SynchronizedSource.TrySetResult();
                return;
            }
            cacheEntry = UpdateCache(cache, cacheKey, cacheValue, value, cachedComputed);
        }

        // 8. Create the new computed - it invalidates the cached one upon registering
        var computed = NewRemoteComputed(input.MethodDef.ComputedOptions, input, result, cacheEntry, call);
        computed.RenewTimeouts(true);
        remoteCachedComputed.SynchronizedSource.TrySetResult();
    }

    // Protected methods

    protected async ValueTask<(Result Result, RpcOutboundComputeCall? Call)> SendRpcCall(
        ComputeMethodInput input,
        RpcPeer peer,
        RpcCacheInfoCapture? cacheInfoCapture,
        CancellationToken cancellationToken)
    {
        var invocation = input.Invocation;
        var proxy = (IProxy)invocation.Proxy;
        var remoteComputeServiceInterceptor = (RemoteComputeServiceInterceptor)proxy.Interceptor;
        var rpcRoutingInterceptor = remoteComputeServiceInterceptor.RpcServiceInterceptor;

        var ctIndex = input.MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0 && invocation.Arguments.GetCancellationToken(ctIndex) != cancellationToken) {
            // Fixing invocation: set CancellationToken + Context
            var arguments = invocation.Arguments.Duplicate();
            arguments.SetCancellationToken(ctIndex, cancellationToken);
            invocation = invocation.With(arguments);
        }

        RpcOutboundComputeCall? call = null;
        try {
            var context = new RpcOutboundContext(RpcComputeCallType.Id) {
                Peer = peer,
                CacheInfoCapture = cacheInfoCapture,
            };
            var callOptions = new RpcCallOptions() {
                AllowRerouting = false,
                AssumeConnected = false,
            };
            using (callOptions.Activate())
            using (context.Activate()) {
                // No "await" inside this block!
                _ = input.MethodDef.InterceptorAsyncInvoker.Invoke(rpcRoutingInterceptor, invocation);
            }
            call = context.Call as RpcOutboundComputeCall;
            if (call is null) {
                Log.LogWarning(
                    "SendRpcCall({Input}, {Peer}, ...) got null call somehow - will try to reroute...",
                    input, peer);
                throw RpcRerouteException.MustRerouteToLocal();
            }

            var resultTask = call.ResultTask;
            if (resultTask.IsCompletedSuccessfully())
                return (Result.NewUntyped(resultTask.GetAwaiter().GetResult()), call);

            var result = await resultTask.ConfigureAwait(false);
            return (Result.NewUntyped(result), call);
        }
        catch (Exception e) {
            return (Result.NewUntypedError(e), call);
        }
    }

    protected RpcCacheEntry? UpdateCache(
        IRemoteComputedCache cache,
        RpcCacheKey key,
        RpcCacheValue? value,
        object? deserializedValue,
        Computed? existing = null)
    {
        var updateLogLevel = LogCacheEntryUpdateSettings.LogLevel;
        if (existing is not null && CacheLog.IfEnabled(updateLogLevel) is { } cacheLog) {
            if (LogCacheEntryUpdateSettings.MaxDataLength is var maxDataLength and > 0)
                cacheLog.Log(updateLogLevel, "Entry update: {Input}, value: {OldValue} -> {NewValue}",
                    existing.Input, ((IRemoteComputed)existing).CacheEntry?.Value.ToString(maxDataLength), value?.ToString(maxDataLength));
            else
                cacheLog.Log(updateLogLevel, "Entry update: {Input}", existing.Input);
        }

        if (value is null) {
            cache.Remove(key); // Error -> wipe cache entry
            return null;
        }

        cache.Set(key, value);
        return new RpcCacheEntry(key, value, deserializedValue);
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

    protected Task WhenConnectedChecked(
        ComputeMethodInput input, RpcPeer peer, CancellationToken cancellationToken = default)
    {
        if (peer.IsConnected(out var handshake, out _))
            return handshake.RemoteHubId == RpcHub.Id && input.Invocation.Proxy is not InterfaceProxy
                ? Task.FromException(Errors.RemoteComputeMethodCallFromTheSameService(RpcMethodDef, peer.Ref))
                : Task.CompletedTask;

        return WhenConnectedCheckedAsync(input, peer, RpcMethodDef, cancellationToken);

        static async Task WhenConnectedCheckedAsync(
            ComputeMethodInput input, RpcPeer peer, RpcMethodDef methodDef, CancellationToken cancellationToken)
        {
            var (handshake, _) = await peer
                .WhenConnected(methodDef.Timeouts.ConnectTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (handshake.RemoteHubId == methodDef.Hub.Id && input.Invocation.Proxy is not InterfaceProxy)
                throw Errors.RemoteComputeMethodCallFromTheSameService(methodDef, peer.Ref);
        }
    }

    // InvalidateXxx

    protected Task InvalidateOnError(Computed computed, Exception? error, string source)
    {
        if (error is RpcRerouteException)
            return InvalidateToReroute(computed, error, source);

        InvalidateToProduceError(computed, error, source);
        return Task.CompletedTask;
    }

    protected void InvalidateToProduceError(Computed computed, Exception? error, string source)
    {
        Log.LogWarning(error, "Invalidating to produce error: {Input}", computed.Input);
        computed.Invalidate(immediately: true, new InvalidationSource(source));
    }

    protected async Task InvalidateToReroute(Computed computed, Exception? error, string source)
    {
        Log.LogWarning(error, "Invalidating to reroute: {Input}", computed.Input);
        await RpcMethodDef.Hub.InternalServices.RerouteDelayer.Invoke(default).ConfigureAwait(false);
        computed.Invalidate(immediately: true, new InvalidationSource(source));
    }

    // Abstract methods

    protected abstract Computed NewReplicaComputed(ComputeMethodInput input);

    protected abstract Computed NewRemoteComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result output,
        RpcCacheEntry? cacheEntry,
        RpcOutboundComputeCall? call = null);
}
