namespace ActualLab.Redis.Internal;

/// <summary>
/// Factory methods for Redis-specific exceptions.
/// </summary>
public static class Errors
{
    public static Exception SourceStreamError()
        => new InvalidOperationException("Source stream completed with an error.");
}
