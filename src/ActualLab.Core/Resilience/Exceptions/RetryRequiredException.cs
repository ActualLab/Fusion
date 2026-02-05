namespace ActualLab.Resilience;

/// <summary>
/// A super-transient exception indicating that a retry is required unconditionally.
/// </summary>
[Serializable]
public class RetryRequiredException : TransientException, ISuperTransientException
{
    public RetryRequiredException()
        : this(null) { }
    public RetryRequiredException(string? message)
        : base(message ?? "Retry required.") { }
    public RetryRequiredException(string? message, Exception innerException)
        : base(message ?? "Retry required.", innerException) { }

    [Obsolete("Obsolete")]
    protected RetryRequiredException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
