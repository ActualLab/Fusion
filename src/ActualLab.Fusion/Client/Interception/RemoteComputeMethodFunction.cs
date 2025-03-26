using Cysharp.Text;
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

public interface IRemoteComputeMethodFunction : IComputeMethodFunction
{
    public RpcHub RpcHub { get; }
    public RpcMethodDef RpcMethodDef { get; }
    public RpcSafeCallRouter RpcCallRouter { get; }
    public IRemoteComputedCache? RemoteComputedCache { get; }
    public object? LocalTarget { get; }

    public object? RemoteComputeServiceInterceptorHandler(Invocation invocation);
}

public class RemoteComputeMethodFunction<T>(
    FusionHub hub,
    ComputeMethodDef methodDef,
    RpcMethodDef rpcMethodDef,
    object? localTarget
    ) : ComputeMethodFunction<T>(hub, methodDef), IRemoteComputeMethodFunction
{
    private string? _toString;

    protected readonly (LogLevel LogLevel, int MaxDataLength) LogCacheEntryUpdateSettings =
        hub.RemoteComputeServiceInterceptorOptions.LogCacheEntryUpdateSettings;
    protected ILogger CacheLog => Hub.RemoteComputedCacheLog;

    public readonly RpcHub RpcHub = hub.RpcHub;
    public readonly RpcMethodDef RpcMethodDef = rpcMethodDef;
    public readonly RpcSafeCallRouter RpcCallRouter = hub.RpcHub.InternalServices.CallRouter;
    public readonly IRemoteComputedCache? RemoteComputedCache = hub.RemoteComputedCache;
    public readonly object? LocalTarget = localTarget;

    // IRemoteComputeMethodFunction implementation
    RpcHub IRemoteComputeMethodFunction.RpcHub => RpcHub;
    RpcMethodDef IRemoteComputeMethodFunction.RpcMethodDef => RpcMethodDef;
    RpcSafeCallRouter IRemoteComputeMethodFunction.RpcCallRouter => RpcCallRouter;
    IRemoteComputedCache? IRemoteComputeMethodFunction.RemoteComputedCache => RemoteComputedCache;
    object? IRemoteComputeMethodFunction.LocalTarget => LocalTarget;

    public override string ToString()
        => _toString ??= ZString.Concat('*', base.ToString());

    public object? RemoteComputeServiceInterceptorHandler(Invocation invocation)
    {
        var input = new ComputeMethodInput(this, MethodDef, invocation);
        var cancellationToken = invocation.Arguments.GetCancellationToken(CancellationTokenIndex); // Auto-handles -1 index
        try {
            // Inlined:
            // var task = function.InvokeAndStrip(input, ComputeContext.Current, cancellationToken);
            var context = ComputeContext.Current;
            var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
            var synchronizer = RemoteComputedSynchronizer.Current;
            var task = !ReferenceEquals(synchronizer, null) && (context.CallOptions & CallOptions.GetExisting) == 0
                ? UseOrComputeWithSynchronizer()
                : ComputedImpl.TryUseExisting(computed, context)
                    ? ComputedImpl.StripToTask(computed, context)
                    : TryRecompute(input, context, cancellationToken);
            // ReSharper disable once HeapView.BoxingAllocation
            return MethodDef.ReturnsValueTask ? new ValueTask<T>(task) : task;

            async Task<T> UseOrComputeWithSynchronizer() {
                // If we're here, (context.CallOptions & CallOptions.GetExisting) == 0,
                // which means that only CallOptions.Capture can be used.

                if (computed == null || !computed.IsConsistent())
                    computed = await TryRecomputeForSyncAwaiter(input, cancellationToken).ConfigureAwait(false);
                var whenSynchronized = synchronizer.WhenSynchronized(computed, cancellationToken);
                if (!whenSynchronized.IsCompletedSuccessfully()) {
                    await whenSynchronized.ConfigureAwait(false);
                    if (!computed.IsConsistent())
                        computed = await computed.Update(cancellationToken).ConfigureAwait(false);
                }

                // Note that until this moment UseNew(...) wasn't called for computed!
                ComputedImpl.UseNew(computed, context);
                return computed.Value;
            }
        }
        finally {
            if (cancellationToken.CanBeCanceled)
                // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                invocation.Arguments.SetCancellationToken(CancellationTokenIndex, default);
        }
    }

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
                    // Local compute / no RPC call scenario
                    var computed = new ReplicaComputed<T>(ComputedOptions, typedInput);
                    using var _ = Computed.BeginCompute(computed);
                    // LocalTarget != null -> proxy is a DistributedPair service & the Service.Method is invoked
                    if (LocalTarget != null) {
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

                    // LocalTarget == null -> proxy is either:
                    // - a pure client (interface proxy), so InvokeIntercepted will fail for it
                    //   (there is no base.Method)
                    // - or a Distributed mode service, so its base.Method should be invoked
                    try {
                        var result = InvokeIntercepted(typedInput, cancellationToken);
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

                // RPC call scenario
                try {
                    var cache = GetCache(typedInput);
                    // existing != null -> it's invalidated, and since cached value is even more outdated,
                    // it doesn't make sense to fetch it
                    return existing == null && cache != null
                        ? await ComputeCachedOrRpc(typedInput, cache, peer, cancellationToken)
                            .ConfigureAwait(false)
                        : await ComputeRpc(typedInput, cache, (RemoteComputed<T>)existing!, peer, cancellationToken)
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

    public async Task<Computed<T>> ComputeRpc(
        ComputeMethodInput input,
        IRemoteComputedCache? cache,
        RemoteComputed<T>? existing,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        // SendRpcCall uses an interceptor with AssumeConnected == false,
        // so we await for the connection here.
        var whenConnected = WhenConnectedChecked(input, peer, cancellationToken);
        if (!whenConnected.IsCompletedSuccessfully()) // Slow path
            await whenConnected.ConfigureAwait(false);

        var existingCacheEntry = existing?.CacheEntry;
        var cacheInfoCapture = cache != null
            ? new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash)
            : null;
        var (result, call) = await SendRpcCall(input, peer, cacheInfoCapture, cancellationToken).ConfigureAwait(false);
        if (result.Error is OperationCanceledException e) // Also handles RpcRerouteException
            throw e; // We treat server-side cancellations the same way as client-side cancellations

        RpcCacheEntry? cacheEntry = null;
        if (cacheInfoCapture != null && cacheInfoCapture.HasKeyAndValue(out var cacheKey, out var cacheValueSource)) {
            // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
            var cacheValue = (await cacheValueSource.Task.ResultAwait(false)).ValueOrDefault; // None if error

            if (existingCacheEntry == null)
                cacheEntry = UpdateCache(cache!, cacheKey, cacheValue, result.ValueOrDefault!);
            else {
                if (!cacheValue.IsNone && cacheValue.HashOrDataEquals(existingCacheEntry.Value))
                    cacheEntry = existingCacheEntry; // Existing cached entry is still intact
                else
                    cacheEntry = UpdateCache(cache!, cacheKey, cacheValue, result.ValueOrDefault!, existing);
            }
        }

        var computed = new RemoteComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call!);
        existing?.SynchronizedSource.TrySetResult();
        return computed;
    }

    public async ValueTask<Computed<T>> ComputeCachedOrRpc(
        ComputeMethodInput input,
        IRemoteComputedCache cache,
        RpcPeer peer,
        CancellationToken cancellationToken)
    {
        var cacheInfoCapture = new RpcCacheInfoCapture(RpcCacheInfoCaptureMode.KeyOnly);
        // This is a fake call that only captures cache key.
        // No actual RPC call happens here, and SendRpcCall completes synchronously here.
        var sendTask = SendRpcCall(input, peer, cacheInfoCapture, cancellationToken);
        if (!sendTask.IsCompleted)
            throw ActualLab.Internal.Errors.InternalError($"{nameof(SendRpcCall)} must complete synchronously here.");

        if (cacheInfoCapture.Key is not { } cacheKey) {
            // cacheKey wasn't captured - a weird case that normally shouldn't happen.
            // The best we can do here is to proceed assuming cache entry is missing,
            // i.e. perform RPC call & update cache.
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);
        }

        var cacheEntry = await cache.Get<T>(input, cacheKey, cancellationToken).ConfigureAwait(false);
        if (cacheEntry == null)
            // No cacheResult wasn't captured -> perform RPC call & update cache
            return await ComputeRpc(input, cache, null, peer, cancellationToken).ConfigureAwait(false);

        var cachedComputed = new RemoteComputed<T>(
            input.MethodDef.ComputedOptions,
            input, cacheEntry.Result,
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
        RemoteComputed<T> cachedComputed,
        RpcPeer peer)
    {
        // 0. Await for RPC call delay
        var delayTask = Caching.RemoteComputedCache.UpdateDelayer?.Invoke(input, peer);
        if (delayTask != null && delayTask.IsCompletedSuccessfully())
            await delayTask.ConfigureAwait(false);

        // 1. Await for the connection
        // SendRpcCall uses an interceptor with AssumeConnected == false, so we have to do it here.
        var whenConnected = WhenConnectedChecked(input, peer);
        if (!whenConnected.IsCompletedSuccessfully()) { // Slow path
            try {
                await whenConnected.ConfigureAwait(false);
            }
            catch (Exception whenConnectedError) {
                await InvalidateOnError(cachedComputed, whenConnectedError).ConfigureAwait(false);
                return;
            }
        }

        // 2. Send the RPC call
        var existingCacheEntry = cachedComputed.CacheEntry;
        var cacheInfoCapture = new RpcCacheInfoCapture(existingCacheEntry ?? RpcCacheEntry.RequestHash);
        var (result, call) = await SendRpcCall(input, peer, cacheInfoCapture, default).ConfigureAwait(false);
        if (call == null || result.Error is RpcRerouteException) {
            await InvalidateToReroute(cachedComputed, result.Error).ConfigureAwait(false);
            return;
        }

        // 3. Bind the call to cachedComputed
        if (!cachedComputed.BindToCall(call)) {
            // A weird case: cachedComputed is already invalidated (manually?).
            // This means the call is already aborted (see BindToCall logic),
            // and since we're performing a background update, we can just exit.
            return;
        }

        // 4. Handle OperationCanceledException
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

        // 5. Get cache key & data
        RpcCacheValue cacheValue = default;
        if (cacheInfoCapture.HasKeyAndValue(out var cacheKey, out var cacheValueSource))
            // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
            cacheValue = (await cacheValueSource.Task.ResultAwait(false)).ValueOrDefault; // None if error

        // 6. Re-entering the lock & check if cachedComputed is still consistent
        using var releaser = await InputLocks.Lock(input).ConfigureAwait(false);
        if (!cachedComputed.IsConsistent())
            return; // Since the call was bound to cachedComputed, it's properly cancelled already

        releaser.MarkLockedLocally();

        // 7. Update cache
        RpcCacheEntry? cacheEntry;
        if (existingCacheEntry == null)
            cacheEntry = UpdateCache(cache, cacheKey, cacheValue, result.ValueOrDefault!);
        else {
            if (!cacheValue.IsNone && cacheValue.HashOrDataEquals(existingCacheEntry.Value)) {
                // Existing cached entry is still intact
                cachedComputed.SynchronizedSource.TrySetResult();
                return;
            }
            cacheEntry = UpdateCache(cache, cacheKey, cacheValue, result.ValueOrDefault!, cachedComputed);
        }

        // 8. Create the new computed - it invalidates the cached one upon registering
        var computed = new RemoteComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call);
        computed.RenewTimeouts(true);
        cachedComputed.SynchronizedSource.TrySetResult();
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
        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var existing = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (ComputedImpl.TryUseExistingFromLock(existing, context))
            return ComputedImpl.Strip(existing, context);

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed.Value;
    }

    protected internal virtual async Task<Computed<T>> TryRecomputeForSyncAwaiter(
        ComputedInput input,
        CancellationToken cancellationToken = default)
    {
        // This method does exactly what TryRecompute does, but with two changes:
        // - it assumes ComputeContext.None is used
        // - and returns Computed<T> instead of T.
        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var existing = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
        if (existing != null && existing.IsConsistent())
            return existing;

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        return computed;
    }

    protected async ValueTask<(Result<T> Result, RpcOutboundComputeCall<T>? Call)> SendRpcCall(
        ComputeMethodInput input,
        RpcPeer peer,
        RpcCacheInfoCapture? cacheInfoCapture,
        CancellationToken cancellationToken)
    {
        var context = new RpcOutboundContext(RpcComputeCallType.Id) {
            Peer = peer,
            CacheInfoCapture = cacheInfoCapture,
        };
        var invocation = input.Invocation;
        var proxy = (IProxy)invocation.Proxy;
        var remoteComputeServiceInterceptor = (RemoteComputeServiceInterceptor)proxy.Interceptor;
        var computeCallInterceptor = remoteComputeServiceInterceptor.ComputeCallInterceptor;

        var ctIndex = input.MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0 && invocation.Arguments.GetCancellationToken(ctIndex) != cancellationToken) {
            // Fixing invocation: set CancellationToken + Context
            var arguments = invocation.Arguments.Duplicate();
            arguments.SetCancellationToken(ctIndex, cancellationToken);
            invocation = invocation.With(arguments, context);
        }
        else {
            // Nothing to fix: it's the same cancellation token or there is no token
            invocation = invocation.With(context);
        }

        RpcOutboundComputeCall<T>? call = null;
        try {
            _ = input.MethodDef.InterceptorAsyncInvoker.Invoke(computeCallInterceptor, invocation);
            call = context.Call as RpcOutboundComputeCall<T>;
            if (call == null) {
                Log.LogWarning(
                    "SendRpcCall({Input}, {Peer}, ...) got null call somehow - will try to reroute...",
                    input, peer);
                throw RpcRerouteException.MustRerouteToLocal();
            }

            var resultTask = call.ResultTask;
            if (resultTask.IsCompletedSuccessfully())
                return (resultTask.Result, call);

            var result = await resultTask.ConfigureAwait(false);
            return (result, call);
        }
        catch (Exception e) {
            return (Result.NewError<T>(e), call);
        }
    }

    protected RpcCacheEntry? UpdateCache(
        IRemoteComputedCache cache,
        RpcCacheKey key,
        RpcCacheValue value,
        T deserializedValue,
        RemoteComputed<T>? existing = null)
    {
        var updateLogLevel = LogCacheEntryUpdateSettings.LogLevel;
        if (existing != null && CacheLog.IfEnabled(updateLogLevel) is { } cacheLog) {
            if (LogCacheEntryUpdateSettings.MaxDataLength is var maxDataLength and > 0)
                cacheLog.Log(updateLogLevel, "Entry update: {Input}, value: {OldValue} -> {NewValue}",
                    existing.Input, existing.CacheEntry!.Value.ToString(maxDataLength), value.ToString(maxDataLength));
            else
                cacheLog.Log(updateLogLevel, "Entry update: {Input}", existing.Input);
        }

        if (value.IsNone) {
            cache.Remove(key); // Error -> wipe cache entry
            return null;
        }

        cache.Set(key, value);
        return new RpcCacheEntry<T>(key, value, deserializedValue);
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

    protected Task InvalidateOnError(RemoteComputed<T> computed, Exception? error)
    {
        if (error is RpcRerouteException)
            return InvalidateToReroute(computed, error);

        InvalidateToProduceError(computed, error);
        return Task.CompletedTask;
    }

    protected void InvalidateToProduceError(RemoteComputed<T> computed, Exception? error)
    {
        Log.LogWarning(error, "Invalidating to produce error: {Input}", computed.Input);
        computed.Invalidate(true);
    }

    protected async Task InvalidateToReroute(RemoteComputed<T> computed, Exception? error)
    {
        Log.LogWarning(error, "Invalidating to reroute: {Input}", computed.Input);
        await RpcMethodDef.Hub.InternalServices.RerouteDelayer.Invoke(default).ConfigureAwait(false);
        computed.Invalidate(true);
    }
}
