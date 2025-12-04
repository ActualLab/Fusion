namespace ActualLab.Testing.Logging;

public sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
#if NET7_0_OR_GREATER
        where TState : notnull
#endif
        => null!;

    public bool IsEnabled(LogLevel logLevel)
        => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter.Invoke(state, exception);
        provider.UseBuffer(buffer => {
            buffer
                .Append(LogLevelChar(logLevel)).Append(' ')
                .Append(provider.StartedAt.Elapsed.ToShortString()).Append(' ')
                .Append(category).Append(": ")
                .Append(message);
            if (exception != null)
                buffer.AppendLine().Append(exception);
            buffer.AppendLine();
        });
    }

    public static char LogLevelChar(LogLevel logLevel)
        => logLevel switch {
            LogLevel.Trace => 'T',
            LogLevel.Debug => 'D',
            LogLevel.Information => 'I',
            LogLevel.Warning => 'W',
            LogLevel.Error => 'E',
            LogLevel.Critical => '!',
            _ => ' ',
        };
}
