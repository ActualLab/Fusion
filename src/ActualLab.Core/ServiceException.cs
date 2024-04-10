namespace ActualLab;

#pragma warning disable SYSLIB0051

public class ServiceException : Exception
{
    public ServiceException() { }
    public ServiceException(string? message)
        : base(message) { }
    public ServiceException(string? message, Exception? innerException)
        : base(message, innerException) { }

    [Obsolete("Obsolete")]
    protected ServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
