namespace ActualLab.Resilience;

[Serializable]
public class RetryPolicyTimeoutExceededException : TimeoutException
{
    public RetryPolicyTimeoutExceededException()
        : this(message: null, innerException: null) { }
    public RetryPolicyTimeoutExceededException(string? message)
        : this(message, innerException: null) { }
    public RetryPolicyTimeoutExceededException(Exception? innerException)
        : base("Retry policy timeout exceeded.", innerException) { }
    public RetryPolicyTimeoutExceededException(string? message, Exception? innerException)
        : base(message ?? "Retry policy timeout exceeded.", innerException) { }

    [Obsolete("Obsolete")]
    protected RetryPolicyTimeoutExceededException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
