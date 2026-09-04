using System.Security.Cryptography;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

#pragma warning disable CA1822

/// <summary>
/// Configuration options for outbound RPC calls, including timeouts, routing, and hashing.
/// </summary>
public record RpcOutboundCallOptions
{
    public static RpcOutboundCallOptions Default { get; set; } = new();

    public RetryDelaySeq ReroutingDelays { get; init; } = RetryDelaySeq.Exp(0.1, 5);
    // Delegate options
    public Func<RpcMethodDef, RpcCallTimeouts> TimeoutsProvider { get; init; }
    public Func<RpcOutboundCall, RpcPeer, RpcDelayedCallAction> DelayHandler { get; init; }
    public Func<RpcMethodDef, Func<ArgumentList, RpcRef>> RouterFactory { get; init; }
    public Func<RpcMethodDef, int, CancellationToken, Task> ReroutingDelayer { get; init; }
    public Func<ReadOnlyMemory<byte>, string> Hasher { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcOutboundCallOptions()
    {
        TimeoutsProvider = DefaultTimeoutsProvider;
        DelayHandler = DefaultDelayHandler;
        RouterFactory = DefaultRouterFactory;
        ReroutingDelayer = DefaultReroutingDelayer;
        Hasher = DefaultHasher;
    }

    // Protected methods

    protected static Func<ArgumentList, RpcRef> DefaultRouterFactory(RpcMethodDef methodDef)
        => static _ => RpcRef.Default;

    protected static RpcCallTimeouts DefaultTimeoutsProvider(RpcMethodDef methodDef)
    {
        var defaultTimeouts = RpcCallTimeouts.Default.Get(methodDef);
        if (methodDef.Attribute is not { } attribute)
            return defaultTimeouts;

        // NaN = inherit the kind default; +Inf = TimeSpanExt.Infinite = no timeout.
        var connectTimeout = attribute.ConnectTimeout is double.NaN
            ? defaultTimeouts.ConnectTimeout : attribute.ConnectTimeout.AsTimeout();
        var runTimeout = attribute.RunTimeout is double.NaN
            ? defaultTimeouts.RunTimeout : attribute.RunTimeout.AsTimeout();
        var cacheFallbackDelay = attribute.CacheFallbackDelay is double.NaN
            ? defaultTimeouts.CacheFallbackDelay : attribute.CacheFallbackDelay.AsTimeout();
        var delayTimeout = attribute.DelayTimeout is double.NaN
            ? defaultTimeouts.DelayTimeout : attribute.DelayTimeout.AsTimeout();
        return new RpcCallTimeouts(connectTimeout, runTimeout) {
            CacheFallbackDelay = cacheFallbackDelay,
            DelayTimeout = delayTimeout,
        };
    }

    protected static RpcDelayedCallAction DefaultDelayHandler(RpcOutboundCall call, RpcPeer peer)
    {
        var methodDef = call.MethodDef;
        var action = methodDef.Attribute?.DelayAction ?? RpcDelayedCallAction.Default;
        return action.Or(methodDef.GetDefaultDelayedCallAction());
    }

    protected static Task DefaultReroutingDelayer(RpcMethodDef methodDef, int failureCount, CancellationToken cancellationToken)
    {
        var outboundCallOptions = methodDef.Hub.OutboundCallOptions;
        return Task.Delay(outboundCallOptions.ReroutingDelays.GetDelay(failureCount), cancellationToken);
    }

    protected static string DefaultHasher(ReadOnlyMemory<byte> bytes)
    {
        // It's better to use a more efficient hash function here, e.g., Blake3.
        // We use SHA256 mainly to minimize the number of dependencies.
#if NET5_0_OR_GREATER
        var buffer = (Span<byte>)stackalloc byte[32]; // 32 bytes
        SHA256.HashData(bytes.Span, buffer);
        return Convert.ToBase64String(buffer[..18]); // 18 bytes -> 24 chars
#else
        using var sha256 = SHA256.Create();
        var buffer = sha256.ComputeHash(bytes.TryGetUnderlyingArray() ?? bytes.ToArray()); // 32 bytes
        return Convert.ToBase64String(buffer.AsSpan(0, 18).ToArray()); // 18 bytes -> 24 chars
#endif
    }
}
