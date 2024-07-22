namespace ActualLab.Versioning;

#pragma warning disable SYSLIB0051

[Serializable]
public class VersionMismatchException : Exception
{
    public VersionMismatchException()
        : this(message: null, innerException: null) { }
    public VersionMismatchException(string? message)
        : this(message, innerException: null) { }
    public VersionMismatchException(string? message, Exception? innerException)
        : base(message ?? "Version mismatch.", innerException) { }

    [Obsolete("Obsolete")]
    protected VersionMismatchException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
