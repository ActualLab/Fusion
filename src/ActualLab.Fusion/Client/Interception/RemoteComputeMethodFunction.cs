using System.Diagnostics;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Diagnostics;
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

/// <summary>
/// A strongly-typed <see cref="RemoteComputeMethodFunction"/> that creates
/// <see cref="RemoteComputed{T}"/> instances for remote compute method calls.
/// </summary>
public sealed class RemoteComputeMethodFunction<T>(
    FusionHub hub,
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef
    ) : RemoteComputeMethodFunction(hub, methodDef, rpcMethodDef)
{
    protected override Computed NewComputed(ComputeMethodInput input)
        => new ComputeMethodComputed<T>(ComputedOptions, input);

    protected override Computed NewRemoteComputed(ComputeMethodInput input, Result output, RpcCacheEntry? cacheEntry, RpcOutboundComputeCall? call = null)
        => new RemoteComputed<T>(ComputedOptions, input, output, cacheEntry, call);

    protected override IRemoteComputedCache NewDefaultValueCache()
        => new DefaultValueRemoteComputedCache(default(T));
}

/// <summary>
/// A <see cref="ComputeMethodFunction"/> that handles remote (RPC) compute method calls,
/// with support for caching, synchronization, and rerouting.
/// </summary>
public abstract class RemoteComputeMethodFunction(
    FusionHub hub,
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef
    ) : ComputeMethodFunction(hub, methodDef)
{
    private string? _toString;
    private IRemoteComputedCache? _defaultValueCache;

    protected readonly (LogLevel LogLevel, int MaxDataLength) LogCacheEntryUpdateSettings
        = hub.RemoteComputeServiceInterceptorOptions.LogCacheEntryUpdateSettings;
    protected ILogger CacheLog => Hub.RemoteComputedCacheLog;

    public readonly RpcHub RpcHub = hub.RpcHub;
    public readonly RpcMethodDef RpcMethodDef = rpcMethodDef;
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
        var rerouteCount = 0;
        var startedAt = CpuTimestamp.Now;
        var context = ComputeContext.Current;

        // If we're here, it's either a client or distributed service, i.e., it can't be a pure server.
        // So the only possible routing modes are Inbound and Outbound, but not Prerouted.
        var routingMode = (context.CallOptions & CallOptions.InboundRpc) != 0
            ? RpcRoutingMode.Inbound
            : RpcRoutingMode.Outbound;
        while (true) {
            try {
                var peer = RpcMethodDef.RouteCall(typedInput.Invocation.Arguments, routingMode);
                peer.Route.RerouteIfChanged();

                if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                    // Local computation / no RPC call scenario
                    // Proxy is either:
                    // - a pure client (interface proxy), so InvokeIntercepted will fail for it
                    //   (there is no base.Method)
                    // - or a Distributed mode service, so its base.Method should be invoked
                    var computed = NewComputed(typedInput);
                    using var _ = Computed.BeginCompute(computed);
                    try {
                        var route = peer.Route;
                        var linkedCts = await route
                            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                            .PrepareLocalExecution(RpcMethodDef, addDependency: true, cancellationToken)
                            .ConfigureAwait(false);
                        try {
                            var result = await typedInput
                                .InvokeInterceptedUntyped(linkedCts?.Token ?? cancellationToken)
                                .ConfigureAwait(false);
                            computed.TrySetValue(result);
                            return computed;
                        }
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        catch (OperationCanceledException e) when (route.MustConvertToRpcRerouteException(e, linkedCts, cancellationToken)) {
                            throw RpcRerouteException.MustReroute();
                        }
                        finally {
                            linkedCts.CancelAndDisposeSilently();
                        }
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
                Services.ThrowIfDisposedOrDisposing();
                ++rerouteCount;
                Log.LogWarning("Rerouting #{RerouteCount}: {Input}", rerouteCount, typedInput);
                await RpcHub.InternalServices.OutboundCallOptions.ReroutingDelayer
                    .Invoke(RpcMethodDef, rerouteCount, cancellationToken)
                    .ConfigureAwait(false);
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
        var existingRemoteComputed = existing as IRemoteComputed;
        var existingCacheEntry = existingRemoteComputed?.CacheEntry;

        Task whenConnected;
        if (!peer.ConnectionState.Value.IsConnected(out var handshake, out _)) {
            // Not connected at call time.
            // - No entry: the peer-level wait applies ConnectTimeout, or ReconnectTimeout when this is
            //   a reconnect, and throws on expiry.
            // - With an entry: wait ReconnectTimeout for a fresh value, then serve the entry
            //   (the else branch) - never an error.
            if (existingCacheEntry is null)
                whenConnected = WhenConnectedCheckedAsync(
                    input, peer, RpcMethodDef.OutboundCallTimeouts, cancellationToken);
            else if (await WhenReconnectedChecked(input, peer, cancellationToken).ConfigureAwait(false))
                whenConnected = Task.CompletedTask;
            else {
                // Serve-stale-on-disconnect: the peer didn't come back within ReconnectTimeout, so the
                // cached value is served now, and the call validating it goes out once the peer is back.
                // ApplyRpcUpdate waits for that with no timeout, then either confirms the value in place
                // (the server answers "match") or displaces it with the fresh one.
                var staleComputed = NewStaleComputed(input, existingRemoteComputed!, "connection_check");
                // Suppressed execution context - see ComputeCachedOrRpc
                _ = ExecutionContextExt.Start(
                    ExecutionContextExt.Default,
                    () => ApplyRpcUpdate(input, cache!, staleComputed, peer));
                return staleComputed;
            }
        }
        else
            whenConnected = handshake.RemoteHubId == RpcHub.Id && input.Invocation.Proxy is not InterfaceProxy
                ? Task.FromException(Errors.RemoteComputeMethodCallFromTheSameService(RpcMethodDef, peer.Ref))
                : Task.CompletedTask;
        if (!whenConnected.IsCompletedSuccessfully)
            await whenConnected.ConfigureAwait(false); // May throw RpcRerouteException!

        var cacheInfoCapture = cache is not null
            ? new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash)
            : null;
        var sendTask = SendRpcCall(input, peer, cacheInfoCapture, cancellationToken);

        // In-flight call with an entry: the same wait, looped because the peer can reconnect and drop again.
        // On expiry the call is NOT abandoned - it stays registered, the tracker resends it, and ApplyRpcResult
        // applies its eventual response to the served computed (the "provisional match").
        if (existingCacheEntry is not null && !sendTask.IsCompleted) {
            // The call may be in flight when the connection dies. RpcOutboundCallTracker resends it on
            // reconnect but never times it out, so it's raced against a disconnect: once the peer stays
            // away for ReconnectTimeout, the cached value is served, and the call - still registered,
            // still carrying the entry's hash - validates it whenever its response finally lands.
            var sendTaskAsTask = sendTask.AsTask();
            while (true) {
                var disconnectTask = peer.ConnectionState.Value.WhenDisconnected;
                var winner = await Task.WhenAny(sendTaskAsTask, disconnectTask).ConfigureAwait(false);
                if (winner == sendTaskAsTask)
                    break;

                // If WhenDisconnected faulted (terminal error), rethrow rather than serve stale.
                if (disconnectTask.IsFaulted) {
                    peer.Route.ThrowIfChanged();
                    await disconnectTask.ConfigureAwait(false);
                }
                if (await WhenReconnectedChecked(input, peer, cancellationToken).ConfigureAwait(false))
                    continue;

                var staleComputed = NewStaleComputed(input, existingRemoteComputed!, "active_call");
                // Suppressed execution context - see ComputeCachedOrRpc
                _ = ExecutionContextExt.Start(
                    ExecutionContextExt.Default,
                    () => ApplyRpcResult(input, cache!, staleComputed, sendTaskAsTask.ToValueTask(), cacheInfoCapture!));
                return staleComputed;
            }
            // Assign the completed task back to sendTask, coz we "unwrapped" the old one
            sendTask = sendTaskAsTask.ToValueTask();
        }

        var (result, call) = await sendTask.ConfigureAwait(false);
        var (value, error) = result;
        if (error is OperationCanceledException e) { // Also handles RpcRerouteException
            // WriteLine($"ComputeRpc got OCE: {e}");
            throw e; // We treat server-side cancellations the same way as client-side cancellations
        }

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

        var computed = NewRemoteComputed(input, result, cacheEntry, call);
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

        var cachedComputed = NewRemoteComputed(input, Result.NewUntyped(cacheEntry.DeserializedValue), cacheEntry);

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

        // 1. Await for the connection - with no timeout: there is a value to show already,
        // so a finite ConnectTimeout must never turn it into an error.
        //
        // RpcCallTimeouts.None = infinite wait: a background validation already
        // has a value to show, so no ConnectTimeout/ReconnectTimeout may turn
        // it into an error.
        var whenConnected = WhenConnectedChecked(input, peer, RpcCallTimeouts.None);
        if (!whenConnected.IsCompletedSuccessfully) { // Slow path
            try {
                await whenConnected.ConfigureAwait(false); // May throw RpcRerouteException!
            }
            catch (Exception whenConnectedError) {
                const string reason =
                    $"<FusionRpc>.{nameof(ApplyRpcUpdate)}: {nameof(WhenConnectedChecked)} failure";
                await InvalidateOnError(cachedComputed, whenConnectedError, reason).ConfigureAwait(false);
                return;
            }
        }

        // 2. Send the RPC call
        var existingCacheEntry = ((IRemoteComputed)cachedComputed).CacheEntry;
        var cacheInfoCapture = new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash);
        var sendTask = SendRpcCall(input, peer, cacheInfoCapture, default);
        await ApplyRpcResult(input, cache, cachedComputed, sendTask, cacheInfoCapture).ConfigureAwait(false);
    }

    // The tail of ApplyRpcUpdate, split out so the mid-call fallback above can feed it an already-sent call.
    public async Task ApplyRpcResult(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        Computed cachedComputed,
        ValueTask<(Result Result, RpcOutboundComputeCall? Call)> sendTask,
        RpcCacheInfoCapture cacheInfoCapture)
    {
        // Applies the response to the call sent for cachedComputed: a "match" confirms it in place,
        // a fresh value displaces it with a successor that takes over the call.
        var remoteCachedComputed = (IRemoteComputed)cachedComputed;
        var existingCacheEntry = remoteCachedComputed.CacheEntry;
        var (result, call) = await sendTask.ConfigureAwait(false);
        var (value, error) = result;
        if (call is null) {
            const string reason =
                $"<FusionRpc>.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} requested rerouting (call is null)";
            await InvalidateToReroute(cachedComputed, result.Error, reason).ConfigureAwait(false);
            return;
        }
        if (error is RpcRerouteException) {
            const string reason =
                $"<FusionRpc>.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} requested rerouting ({nameof(RpcRerouteException)})";
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
                $"<FusionRpc>.{nameof(ApplyRpcUpdate)}: {nameof(SendRpcCall)} got server-side cancellation";
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

        // 8. Create the new computed - it invalidates the cached one upon registering.
        // The call is handed off to it here, so invalidating the displaced cachedComputed
        // (which shares the same call) won't poison the call & the new computed (audit item 16).
        call.MarkHandedOff();
        var computed = NewRemoteComputed(input, result, cacheEntry, call);
        // The successor's constructor synchronously displaces & invalidates cachedComputed (or, in the
        // predecessor-already-invalidated race, is itself born invalidated), which consumes the marker.
        Debug.Assert(!call.IsHandOffPending,
            "Hand-off marker must be consumed by the time the successor's constructor returns.");
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
        var remoteComputeServiceInterceptor = (RemoteComputeServiceInterceptor)proxy.Binding.Interceptor;
        var rpcInterceptor = remoteComputeServiceInterceptor.RpcInterceptor;

        var ctIndex = input.MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0 && invocation.Arguments.GetCancellationToken(ctIndex) != cancellationToken) {
            // Fixing invocation: set CancellationToken + Context
            var arguments = invocation.Arguments.Duplicate();
            arguments.SetCancellationToken(ctIndex, cancellationToken);
            invocation = invocation.With(arguments);
        }

        RpcOutboundComputeCall? call = null;
        try {
            var settings = new RpcOutboundCallSetup(peer) {
                CacheInfoCapture = cacheInfoCapture,
            };
            using (settings.Activate()) {
                // No "await" inside this block!
                _ = input.MethodDef.InterceptorAsyncInvoker.Invoke(rpcInterceptor, invocation);
            }
            call = settings.ProducedContext!.Call as RpcOutboundComputeCall;
            if (call is null) { // This should never happen, but it's better to be safe than sorry
                Log.LogWarning(
                    "SendRpcCall({Input}, {Peer}, ...) got null call somehow - will try to reroute...",
                    input, peer);
                throw RpcRerouteException.MustReroute();
            }

            var resultTask = call.ResultTask;
            if (resultTask.IsCompletedSuccessfully)
                return (Result.NewUntyped(resultTask.GetAwaiter().GetResult()), call);

            var result = await resultTask.ConfigureAwait(false);
            return (Result.NewUntyped(result), call);
        }
        catch (Exception e) {
            return (Result.NewUntypedError(e), call);
        }
    }

    // Shared by both stale branches
    protected Computed NewStaleComputed(ComputeMethodInput input, IRemoteComputed existing, string operation)
    {
        var cacheEntry = existing.CacheEntry!;
        var staleComputed = NewRemoteComputed(input, Result.NewUntyped(cacheEntry.DeserializedValue), cacheEntry);
        existing.ChainSynchronizedSourceTo((IRemoteComputed)staleComputed);
        if (FusionInstruments.RemoteComputedCacheStaleValueCount.IfEnabled() is { } staleValueCounter) {
            var tags = new TagList { { "operation", operation } };
            staleValueCounter.Add(1, tags);
        }
        return staleComputed;
    }

    protected RpcCacheEntry? UpdateCache(
        IRemoteComputedCache cache,
        RpcCacheKey key,
        RpcCacheValue? value,
        object? deserializedValue,
        Computed? existing = null)
    {
        // ReturnDefault: the entry is always the default one, whatever the call produced
        if (cache is DefaultValueRemoteComputedCache defaultValueCache)
            return defaultValueCache.NewEntry(key);

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
        => input.MethodDef.ComputedOptions.RemoteComputedCacheMode switch {
            RemoteComputedCacheMode.Cache => RemoteComputedCache,
            RemoteComputedCacheMode.ReturnDefault => _defaultValueCache ??= NewDefaultValueCache(),
            _ => null,
        };

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

    // The with-entry wait: false on ReconnectTimeout expiry (the caller serves the entry);
    // reroute, cancellation and the same-service check propagate as before.
    protected async Task<bool> WhenReconnectedChecked(
        ComputeMethodInput input, RpcPeer peer, CancellationToken cancellationToken)
    {
        // Waits up to ReconnectTimeout for the peer to reconnect; false means the wait timed out
        var timeout = RpcMethodDef.OutboundCallTimeouts.ReconnectTimeout;
        if (timeout <= TimeSpan.Zero)
            return false;

        RpcPeerConnectionState connectionState;
        try {
            connectionState = await peer
                .WhenConnectedOrReroute(timeout, RpcTimeoutKind.Reconnect, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcTimeoutException e) when (e.TimeoutKind == RpcTimeoutKind.Reconnect) {
            return false;
        }
        ThrowIfSameService(input, peer, connectionState.Handshake!);
        return true;
    }

    protected Task WhenConnectedChecked(
        ComputeMethodInput input, RpcPeer peer, RpcCallTimeouts timeouts, CancellationToken cancellationToken = default)
    {
        if (!peer.ConnectionState.Value.IsConnected(out var handshake, out _))
            return WhenConnectedCheckedAsync(input, peer, timeouts, cancellationToken);

        return handshake.RemoteHubId == RpcHub.Id && input.Invocation.Proxy is not InterfaceProxy
            ? Task.FromException(Errors.RemoteComputeMethodCallFromTheSameService(RpcMethodDef, peer.Ref))
            : Task.CompletedTask;
    }

    protected async Task WhenConnectedCheckedAsync(
        ComputeMethodInput input, RpcPeer peer, RpcCallTimeouts timeouts, CancellationToken cancellationToken)
    {
        // WhenConnectedOrReroute may throw RpcRerouteException if the peer's route has changed.
        var connectionState = await peer.WhenConnectedOrReroute(timeouts, cancellationToken).ConfigureAwait(false);
        ThrowIfSameService(input, peer, connectionState.Handshake!);
    }

    protected void ThrowIfSameService(ComputeMethodInput input, RpcPeer peer, RpcHandshake handshake)
    {
        if (handshake.RemoteHubId == RpcMethodDef.Hub.Id && input.Invocation.Proxy is not InterfaceProxy)
            throw Errors.RemoteComputeMethodCallFromTheSameService(RpcMethodDef, peer.Ref);
    }

    // InvalidateXxx

    // InvalidateWhenReconnected is gone: a served entry is now validated by its
    // pending call (match -> synchronized in place, different result ->
    // displaced) instead of being invalidated on reconnect.

    // Completes when the peer transitions to a non-connected state (Handshake is null).
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
        await RpcMethodDef.Hub.InternalServices.OutboundCallOptions.ReroutingDelayer
            .Invoke(RpcMethodDef, 1, default)
            .ConfigureAwait(false);
        computed.Invalidate(immediately: true, new InvalidationSource(source));
    }

    // Abstract methods

    protected abstract Computed NewRemoteComputed(
        ComputeMethodInput input,
        Result output,
        RpcCacheEntry? cacheEntry,
        RpcOutboundComputeCall? call = null);

    protected abstract IRemoteComputedCache NewDefaultValueCache();
}
