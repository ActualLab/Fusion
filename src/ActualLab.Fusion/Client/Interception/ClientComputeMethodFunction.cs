using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Internal;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Versioning;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Client.Interception;

#pragma warning disable VSTHRD103

public interface IClientComputeMethodFunction : IComputeMethodFunction
{ }

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

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    protected override ValueTask<Computed<T>> Compute(
#pragma warning restore IL2046
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var cache = GetCache(typedInput);
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
        var cacheInfoCapture = cache != null ? new RpcCacheInfoCapture() : null;
        var call = SendRpcCall(input, cacheInfoCapture, cancellationToken);

        var result = await call.GetResult(cancellationToken).ConfigureAwait(false);
        var cacheEntry = await UpdateCache(cache!, cacheInfoCapture, result, cancellationToken).ConfigureAwait(false);
        var synchronizedSource = existing?.SynchronizedSource ?? AlwaysSynchronized.Source;
        return new ClientComputed<T>(
            input.MethodDef.ComputedOptions,
            input, result,
            cacheEntry, call, synchronizedSource);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
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

        // 2. Bind the call to cachedComputed
        if (!cachedComputed.BindToCall(call)) {
            // Ok, this is a weird case: existing was invalidated manually while we were getting here.
            // This means the call is already aborted (see BindToCall logic), and since we're
            // operating in background to update cached value, we can just exit.
            return;
        }

        // 3. Await for its completion
        var result = await call.GetResult().ConfigureAwait(false);

        // 4. Get cache key & data
        var (key, data) = await cacheInfoCapture.GetKeyAndData(default).ConfigureAwait(false);

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

    public override async ValueTask<Computed<T>> Invoke(ComputedInput input,
        IComputed? usedBy,
        ComputeContext? context,
        CancellationToken cancellationToken = default)
    {
        context ??= ComputeContext.Current;

        // Read-Lock-RetryRead-Compute-Store pattern

        var existing = GetExisting(input);
        if (existing.TryUseExisting(context, usedBy))
            return existing!;

        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        existing = GetExisting(input);
        if (existing.TryUseExistingFromLock(context, usedBy))
            return existing!;

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy, existing);
        return computed;
    }

    public override Task<T> InvokeAndStrip(ComputedInput input,
        IComputed? usedBy,
        ComputeContext? context,
        CancellationToken cancellationToken = default)
    {
        context ??= ComputeContext.Current;

        var computed = GetExisting(input);
        return computed.TryUseExisting(context, usedBy)
            ? computed.StripToTask(context)
            : TryRecompute(input, usedBy, context, cancellationToken);
    }

    // Protected methods

    protected new async Task<T> TryRecompute(ComputedInput input,
        IComputed? usedBy,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        using var releaser = await InputLocks.Lock(input, cancellationToken).ConfigureAwait(false);

        var existing = GetExisting(input);
        if (existing.TryUseExistingFromLock(context, usedBy))
            return existing.Strip(context);

        releaser.MarkLockedLocally();
        var computed = await Compute(input, existing, cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy, existing);
        return computed.Value;
    }

    // Private methods

    private static RpcOutboundComputeCall<T> SendRpcCall(
        ComputeMethodInput input,
        RpcCacheInfoCapture? cacheInfoCapture,
        CancellationToken cancellationToken)
    {
        using var scope = RpcOutboundContext.Use(RpcComputeCallType.Id);
        scope.Context.CacheInfoCapture = cacheInfoCapture;
        input.InvokeOriginalFunction(cancellationToken);
        var call = (RpcOutboundComputeCall<T>?)scope.Context.Call;
        if (call == null)
            throw Errors.InternalError(
                "No call is sent, which means the service behind this proxy isn't an RPC client proxy (misconfiguration), " +
                "or RpcPeerResolver routes the call to a local service, which shouldn't happen at this point.");
        return call;
    }

    private static RpcCacheEntry? UpdateCache(
        IClientComputedCache cache,
        RpcCacheKey? key,
        TextOrBytes? data)
    {
        if (ReferenceEquals(key, null))
            return null;

        if (!data.HasValue) {
            cache.Remove(key); // Error -> wipe cache entry
            return null;
        }

        cache.Set(key, data.GetValueOrDefault());
        return new RpcCacheEntry(key, data.GetValueOrDefault());
    }

    private static async ValueTask<RpcCacheEntry?> UpdateCache(
        IClientComputedCache cache,
        RpcCacheInfoCapture? cacheInfoCapture,
        Result<T> result,
        CancellationToken cancellationToken)
    {
        if (result.Error is { } error) {
            // No need to await for dataSource.Task in this case
            if (!error.IsCancellationOf(cancellationToken) && cacheInfoCapture?.Key is { } key1)
                cache.Remove(key1); // Error -> wipe cache entry
            return null;
        }

        var (key, data) = await cacheInfoCapture.GetKeyAndData(cancellationToken).ConfigureAwait(false);
        return UpdateCache(cache, key, data);
    }

    private IClientComputedCache? GetCache(ComputeMethodInput input)
        => Cache == null
            ? null :
            input.MethodDef.ComputedOptions.ClientCacheMode != ClientCacheMode.Cache
                ? null
                : Cache;
}
