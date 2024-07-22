using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable RCS1194

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
