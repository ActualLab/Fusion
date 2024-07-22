namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public class LogEntryNotFoundException : KeyNotFoundException
{
    public LogEntryNotFoundException()
        : this(message: null, innerException: null) { }
    public LogEntryNotFoundException(string? message)
        : this(message, innerException: null) { }
    public LogEntryNotFoundException(Exception? innerException)
        : base("Log entry not found.", innerException) { }
    public LogEntryNotFoundException(string? message, Exception? innerException)
        : base(message ?? "Log entry not found.", innerException) { }

    [Obsolete("Obsolete")]
    protected LogEntryNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
