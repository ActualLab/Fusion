namespace ActualLab.Resilience;

#pragma warning disable SYSLIB0051

/// <summary>
/// A tagging interface for any exception that might be "cured" by retrying the operation.
/// </summary>
public interface ITerminalException;

[Serializable]
public class TerminalException : Exception, ITerminalException
{
    public TerminalException()
        : this(null) { }
    public TerminalException(string? message)
        : base(message ?? "Terminal error.") { }
    public TerminalException(string? message, Exception innerException)
        : base(message ?? "Terminal error.", innerException) { }

    [Obsolete("Obsolete")]
    protected TerminalException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
