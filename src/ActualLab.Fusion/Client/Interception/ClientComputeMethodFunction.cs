using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD103

public interface IClientComputeMethodFunction : IComputeMethodFunction;

public class ClientComputeMethodFunction<T>(
    ComputeMethodDef methodDef,
    IClientComputedCache? cache,
    IServiceProvider services
    ) : ComputeFunctionBase<T>(methodDef, services), IClientComputeMethodFunction
{
    private string? _toString;

    protected readonly IClientComputedCache? Cache = cache;

    public override string ToString()
        => _toString ??= ZString.Concat('*', base.ToString());

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    protected override ValueTask<Computed<T>> Compute(
#pragma warning restore IL2046
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var cache = GetCache(typedInput);
        // existing != null -> it's invalidated, so no matter what's cached, we ignore it
        return existing == null && cache != null
            ? ComputeCachedOrRpc(typedInput, cache, cancellationToken)
            : ComputeRpc(typedInput, cache, (ClientComputed<T>)existing!, cancellationToken).ToValueTask();
    }

    private async Task<Computed<T>> ComputeRpc(
        ComputeMethodInput input,
        IClientComputedCache? cache,
        ClientComputed<T>? existing,
        CancellationToken cancellationToken)
    {
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            try {
                var cacheInfoCapture = cache != null ? new RpcCacheInfoCapture() : null;
                var call = SendRpcCall(input, cacheInfoCapture, cancellationToken);
                if (call == null) {
                    // TODO: Properly handle routing to local service
                    throw new RpcRerouteException();
                }

                var result = await call.ResultTask.ResultAwait(false);
                if (result.Error is OperationCanceledException e)
                    throw e; // We treat server-side cancellations the same way as client-side cancellations

                RpcCacheEntry? cacheEntry = null;
                if (cacheInfoCapture != null && cacheInfoCapture.HasKeyAndData(out var key, out var dataSource)) {
                    // dataSource.Task should be already completed at this point, so no WaitAsync(cancellationToken)
                    var dataResult = await dataSource.Task.ResultAwait(false);
                    var data = dataResult.IsValue(out var vData) ? (TextOrBytes?)vData : null;
                    cacheEntry = UpdateCache(cache!, key, data);
                }

                var synchronizedSource = existing?.SynchronizedSource ?? AlwaysSynchronized.Source;
                return new ClientComputed<T>(
                    input.MethodDef.ComputedOptions,
                    input, result,
                    cacheEntry, call, synchronizedSource);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested) {
                var cancellationReprocessingOptions = input.MethodDef.ComputedOptions.CancellationReprocessing;
                if (++tryIndex > cancellationReprocessingOptions.MaxTryCount)
                    throw;
                if (startedAt.Elapsed > cancellationReprocessingOptions.MaxDuration)
                    throw;

                var delay = cancellationReprocessingOptions.RetryDelays[tryIndex];
                Log.LogWarning(e,
                    "ComputeRpc #{TryIndex} was cancelled on the server side for {Category}, will retry in {Delay}",
                    tryIndex, input.Category, delay.ToShortString());
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async ValueTask<Computed<T>> ComputeCachedOrRpc(
        ComputeMethodInput input,
        IClientComputedCache cache,
        CancellationToken cancellationToken)
    {
        var cacheInfoCapture = new RpcCacheInfoCapture(RpcCacheInfoCaptureMode.KeyOnly);
        // This is a fake call that only captures the cache key.
        // No actual call happens at this point.
        SendRpcCall(input, cacheInfoCapture, cancellationToken);
        if (cacheInfoCapture.Key is not { } cacheKey)
            return await ComputeRpc(input, cache, null, cancellationToken).ConfigureAwait(false);

        var cacheResultOpt = await cache.Get<T>(input, cacheKey, cancellationToken).ConfigureAwait(false);
        if (cacheResultOpt is not { } cacheResult)
            return await ComputeRpc(input, cache, null, cancellationToken).ConfigureAwait(false);

        var cacheEntry = new RpcCacheEntry(cacheKey, cacheResult.Data);
        var cachedComputed = new ClientComputed<T>(
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
            () => ApplyRpcUpdate(input, cache, cachedComputed));
        return cachedComputed;
    }

    private async Task ApplyRpcUpdate(
        ComputeMethodInput input,
        IClientComputedCache cache,
        ClientComputed<T> cachedComputed)
    {
        // 1. Start the RPC call
        var cacheInfoCapture = new RpcCacheInfoCapture();
        var call = SendRpcCall(input, cacheInfoCapture, default);
        if (call == null) {
            cachedComputed.Invalidate(true);
            return;
        }

        // 2. Bind the call to cachedComputed
        if (!cachedComputed.BindToCall(call)) {
            // Ok, this is a weird case: existing was invalidated manually while we were getting here.
            // This means the call is already aborted (see BindToCall logic), and since we're
            // operating in background to update cached value, we can just exit.
            return;
        }

        // 3. Await for its completion
        var result = await call.ResultTask.ResultAwait(false);
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
        var synchronizedSource = cachedComputed.SynchronizedSource;
        if (cachedComputed.CacheEntry is { } oldEntry && data?.DataEquals(oldEntry.Data) == true) {
            // Existing cached entry is still intact
            synchronizedSource.TrySetResult(default);
            return;
        }

        // 5. Now, let's update cache entry
        var cacheEntry = UpdateCache(cache, key, data);

        // 6. Create the new computed - it invalidates the cached one upon registering
        var computed = new ClientComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call, synchronizedSource);
        computed.RenewTimeouts(true);
    }

    public override async ValueTask<Computed<T>> Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        // Double-check locking
        var computed = GetExisting(input);
        if (computed.TryUseExisting(context))
            return computed!;

        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        computed = GetExisting(input);
        if (computed.TryUseExistingFromLock(context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await Compute(input, computed, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed;
    }

    public override Task<T> InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var computed = GetExisting(input);
        return computed.TryUseExisting(context)
            ? computed.StripToTask(context)
            : TryRecompute(input, context, cancellationToken);
    }

    // Protected methods

    protected new async Task<T> TryRecompute(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var existing = GetExisting(input);
        if (existing.TryUseExistingFromLock(context))
            return existing.Strip(context);

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed.Value;
    }

    // Private methods

    private static RpcOutboundComputeCall<T>? SendRpcCall(
        ComputeMethodInput input,
        RpcCacheInfoCapture? cacheInfoCapture,
        CancellationToken cancellationToken)
    {
        var context = new RpcOutboundContext(RpcComputeCallType.Id) {
            CacheInfoCapture = cacheInfoCapture
        };
        var invocation = input.Invocation;
        var proxy = (IProxy)invocation.Proxy;
        var interceptor = proxy.Interceptor;
        var ctIndex = input.MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0 && invocation.Arguments.GetCancellationToken(ctIndex) != cancellationToken) {
            var arguments = invocation.Arguments with { }; // Cloning
            arguments.SetCancellationToken(ctIndex, cancellationToken);
            invocation = invocation with { Context = context, Arguments = arguments };
        }
        else
            invocation = invocation with { Context = context };

        if (interceptor is RpcHybridInterceptor hybridInterceptor) {
            // TODO: Handle the routing between the local service & client
            // var rpcMethodDef = (RpcMethodDef)hybridInterceptor.GetMethodDef(invocation.Method, invocation.Proxy.GetType())!;
            interceptor = hybridInterceptor.ClientInterceptor;
        }
        var clientInterceptor = ((ClientComputeServiceInterceptor)interceptor).ClientInterceptor;
        clientInterceptor.ChainIntercept<T>(input.MethodDef, invocation);
        return (RpcOutboundComputeCall<T>?)context.Call;
    }

    private static RpcCacheEntry? UpdateCache(
        IClientComputedCache cache,
        RpcCacheKey? key,
        TextOrBytes? data)
    {
        if (key is not { IsValid: true })
            return null;

        if (!data.HasValue) {
            cache.Remove(key); // Error -> wipe cache entry
            return null;
        }

        cache.Set(key, data.GetValueOrDefault());
        return new RpcCacheEntry(key, data.GetValueOrDefault());
    }

    private IClientComputedCache? GetCache(ComputeMethodInput input)
        => Cache == null
            ? null :
            input.MethodDef.ComputedOptions.ClientCacheMode != ClientCacheMode.Cache
                ? null
                : Cache;
}
