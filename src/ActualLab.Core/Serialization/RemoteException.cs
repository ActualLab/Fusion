using ActualLab.Resilience;

namespace ActualLab.Serialization;

/// <summary>
/// Represents an exception that was serialized from a remote service
/// and reconstructed from <see cref="ExceptionInfo"/>.
/// </summary>
#pragma warning disable SYSLIB0051

[Serializable]
public class RemoteException : Exception, ITransientException
{
#pragma warning disable CA2235
    public ExceptionInfo ExceptionInfo { get; }
#pragma warning restore CA2235

    public RemoteException()
        : this(ExceptionInfo.None)  { }
    public RemoteException(string message)
        : this(ExceptionInfo.None, message) { }
    public RemoteException(string message, Exception innerException)
        : this(ExceptionInfo.None, message, innerException) { }

    public RemoteException(ExceptionInfo exceptionInfo)
        => ExceptionInfo = exceptionInfo;
    public RemoteException(ExceptionInfo exceptionInfo, string message) : base(message)
        => ExceptionInfo = exceptionInfo;
    public RemoteException(ExceptionInfo exceptionInfo, string message, Exception innerException)
        : base(message, innerException)
        => ExceptionInfo = exceptionInfo;

    [Obsolete("Obsolete")]
    protected RemoteException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
