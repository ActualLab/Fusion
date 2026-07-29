namespace ActualLab.Compliance;

/// <summary>
/// A logger that resumes <see cref="Sanitization"/> around each log call, so
/// <see cref="ISanitized"/> arguments render their masked form via <c>ToString()</c>.
/// </summary>
/// <remarks>
/// Sanitization is suspended by default precisely so that a masking member - one written as
/// <c>get =&gt; Sanitizer.MaybeSanitize&lt;T&gt;(field)</c> - stays readable to serializers,
/// which go through the same getter. Turning it on for the duration of the log call, and only
/// on the calling thread, is what keeps masking out of the wire format.
/// </remarks>
public sealed class SanitizingLogger(ILogger inner) : ILogger
{
    public ILogger Inner { get; } = inner;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // The scope must cover the inner Log call, not just the formatter: a provider is free to
        // defer formatting, but every provider that does it inline does it here
        using var _ = Sanitization.Begin();
        Inner.Log(logLevel, eventId, state, exception, formatter);
    }

    public bool IsEnabled(LogLevel logLevel)
        => Inner.IsEnabled(logLevel);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => Inner.BeginScope(state);
}
