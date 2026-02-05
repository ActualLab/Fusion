using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable RCS1194

/// <summary>
/// The exception thrown when a database operation fails during commit or processing.
/// </summary>
[Serializable]
public class DbOperationFailedException : DbUpdateException
{
    public DbOperationFailedException() { }
    public DbOperationFailedException(string message)
        : base(message) { }
    public DbOperationFailedException(string message, Exception innerException)
        : base(message, innerException) { }

    [Obsolete("Obsolete")]
    protected DbOperationFailedException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
