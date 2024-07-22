namespace ActualLab.Internal;

[Serializable]
public class InternalError : Exception
{
    public InternalError() : base("Internal error.") { }
    public InternalError(string? message) : base(message) { }
    public InternalError(string? message, Exception? innerException) : base(message, innerException) { }

    [Obsolete("Obsolete")]
    protected InternalError(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
