using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

/// <summary>
/// Thrown when one of the RPC timeouts expires; <see cref="TimeoutKind"/> tells which one.
/// </summary>
[Serializable]
public class RpcTimeoutException : TimeoutException, ITransientException
{
    private const string DefaultMessage = "RPC timeout.";

    // Unknown for an exception transferred from the remote side: only its type and message travel
    public RpcTimeoutKind TimeoutKind { get; }

    public RpcTimeoutException()
        : this(RpcTimeoutKind.Unknown) { }
    public RpcTimeoutException(string? message)
        : this(RpcTimeoutKind.Unknown, message) { }
    public RpcTimeoutException(string? message, Exception? innerException)
        : this(RpcTimeoutKind.Unknown, message, innerException) { }
    public RpcTimeoutException(RpcTimeoutKind timeoutKind, string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
        => TimeoutKind = timeoutKind;

    [Obsolete("Obsolete")]
    protected RpcTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
