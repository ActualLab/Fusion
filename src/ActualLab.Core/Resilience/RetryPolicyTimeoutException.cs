namespace ActualLab.Resilience;

[Serializable]
public class RetryPolicyTimeoutException : TimeoutException
{
    public RetryPolicyTimeoutException()
        : this(message: null, innerException: null) { }
    public RetryPolicyTimeoutException(string? message)
        : this(message, innerException: null) { }
    public RetryPolicyTimeoutException(Exception? innerException)
        : base("Retry policy timeout.", innerException) { }
    public RetryPolicyTimeoutException(string? message, Exception? innerException)
        : base(message ?? "Retry policy timeout.", innerException) { }

    [Obsolete("Obsolete")]
    protected RetryPolicyTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
