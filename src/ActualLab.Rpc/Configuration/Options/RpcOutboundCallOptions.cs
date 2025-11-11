using System.Security.Cryptography;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcOutboundCallOptions(IServiceProvider services) : RpcServiceBase(services)
{
    public static RetryDelaySeq ReroutingDelays { get; set; } = RetryDelaySeq.Exp(0.1, 5);

    public virtual Func<ArgumentList, RpcPeerRef> CreateRouter(RpcMethodDef methodDef)
        => static _ => RpcPeerRef.Default;


    public virtual Task ReroutingDelay(int failureCount, CancellationToken cancellationToken)
        => Task.Delay(ReroutingDelays.GetDelay(failureCount), cancellationToken);

    public virtual RpcCallTimeoutSet GetTimeouts(RpcMethodDef methodDef)
        => RpcCallTimeoutSet.GetDefault(methodDef);

    public virtual string ComputeHash(ReadOnlyMemory<byte> bytes)
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
