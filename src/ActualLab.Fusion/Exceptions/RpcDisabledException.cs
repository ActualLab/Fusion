namespace ActualLab.Fusion;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcDisabledException : InvalidOperationException
{
    private const string DefaultMessage =
        "All RPC calls are disabled inside the Invalidation.Begin() blocks. " +
        "You can use Computed.BeginIsolatioln() to temporarily disable the invalidation.";

    public RpcDisabledException() : base(DefaultMessage) { }
    public RpcDisabledException(string? message) : base(message ?? DefaultMessage) { }
    public RpcDisabledException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException) { }
    protected RpcDisabledException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
