using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

/// <summary>
/// Thrown when a peer exceeds one of the per-peer count limits declared in <see cref="RpcLimits"/>,
/// which resets the peer.
/// </summary>
[Serializable]
public class RpcResourceLimitExceededException : Exception, ITransientException
{
    private const string DefaultMessage = "RPC resource limit is exceeded.";

    public static RpcResourceLimitExceededException CallCountLimitExceeded(
        RpcRef rpcRef, int inboundCallCount, int outboundCallCount, int limit)
        => new($"'{rpcRef}': call count limit is exceeded: "
            + $"{inboundCallCount} inbound + {outboundCallCount} outbound > {limit}.");

    public static RpcResourceLimitExceededException ObjectCountLimitExceeded(
        RpcRef rpcRef, int sharedObjectCount, int remoteObjectCount, int limit)
        => new($"'{rpcRef}': object count limit is exceeded: "
            + $"{sharedObjectCount} shared + {remoteObjectCount} remote > {limit}.");

    public RpcResourceLimitExceededException()
        : this(message: null, innerException: null) { }
    public RpcResourceLimitExceededException(string? message)
        : this(message, innerException: null) { }
    public RpcResourceLimitExceededException(string? message, Exception? innerException)
        : base(message ?? DefaultMessage, innerException) { }

    [Obsolete("Obsolete")]
    protected RpcResourceLimitExceededException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
