using System.Security.Cryptography;
using ActualLab.Interception;

namespace ActualLab.Rpc;

#pragma warning disable CA1822

public record RpcOutboundCallOptions
{
    public static RpcOutboundCallOptions Default { get; set; } = new();

    public RetryDelaySeq ReroutingDelays { get; init; } = RetryDelaySeq.Exp(0.1, 5);
    // Delegate options
    public Func<RpcMethodDef, RpcCallTimeoutSet> TimeoutsFactory { get; init; }
    public Func<RpcMethodDef, Func<ArgumentList, RpcPeerRef>> RouterFactory { get; init; }
    public Func<RpcOutboundCallOptions, int, CancellationToken, Task> ReroutingDelayFactory { get; init; }
    public Func<ReadOnlyMemory<byte>, string> Hasher { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcOutboundCallOptions()
    {
        TimeoutsFactory = DefaultTimeoutsFactory;
        RouterFactory = DefaultRouterFactory;
        ReroutingDelayFactory = DefaultReroutingDelayFactory;
        Hasher = DefaultHasher;
    }

    public Task GetReroutingDelay(int failureCount, CancellationToken cancellationToken)
        => ReroutingDelayFactory.Invoke(this, failureCount, cancellationToken);

    // Protected methods

    protected static Func<ArgumentList, RpcPeerRef> DefaultRouterFactory(RpcMethodDef methodDef)
        => static _ => RpcPeerRef.Default;

    protected static RpcCallTimeoutSet DefaultTimeoutsFactory(RpcMethodDef methodDef)
        => RpcCallTimeoutSet.Default.Get(methodDef);

    protected static Task DefaultReroutingDelayFactory(RpcOutboundCallOptions options, int failureCount, CancellationToken cancellationToken)
        => Task.Delay(options.ReroutingDelays.GetDelay(failureCount), cancellationToken);

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
