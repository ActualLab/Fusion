namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="ILogger"/> to check enabled log levels.
/// </summary>
public static class LoggerExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLogging([NotNullWhen(true)] this ILogger? log, LogLevel logLevel)
        => log is not null && logLevel != LogLevel.None && log.IsEnabled(logLevel);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ILogger? IfEnabled(this ILogger? log, LogLevel logLevel)
        => IsLogging(log, logLevel) ? log : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ILogger? IfEnabled(this ILogger? log, LogLevel logLevel, bool isEnabled)
        => isEnabled ? log?.IfEnabled(logLevel) : null;
}
