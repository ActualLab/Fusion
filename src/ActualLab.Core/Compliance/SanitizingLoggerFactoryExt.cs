namespace ActualLab.Compliance;

public static class SanitizingLoggerFactoryExt
{
    /// <summary>
    /// Replaces the <see cref="ILoggerFactory"/> registration with one that wraps every logger
    /// into a <see cref="SanitizingLogger"/>, so <see cref="ISanitized"/> values are masked in
    /// the log - and only there.
    /// </summary>
    public static ILoggingBuilder AddSanitizingLoggerFactory(this ILoggingBuilder logging, bool mustSanitize = true)
        => logging.AddSanitizingLoggerFactory(_ => mustSanitize);

    /// <inheritdoc cref="AddSanitizingLoggerFactory(ILoggingBuilder, bool)"/>
    public static ILoggingBuilder AddSanitizingLoggerFactory(
        this ILoggingBuilder logging,
        Func<IServiceProvider, bool> mustSanitizeResolver)
    {
        logging.Services.AddSingleton<ILoggerFactory>(c => {
            var innerFactory = ActivatorUtilities.CreateInstance<LoggerFactory>(c);
            return innerFactory.Sanitizing(mustSanitizeResolver.Invoke(c));
        });
        return logging;
    }

    /// <summary>
    /// Wraps <paramref name="factory"/> into a <see cref="SanitizingLoggerFactory"/>. Use this
    /// where there's no <see cref="ILoggingBuilder"/> to hook - e.g. when assigning
    /// <c>StaticLog.Factory</c> directly.
    /// </summary>
    public static ILoggerFactory Sanitizing(this ILoggerFactory factory, bool mustSanitize = true)
        // Wrapping twice would just open a redundant scope per log call
        => mustSanitize && factory is not SanitizingLoggerFactory
            ? new SanitizingLoggerFactory(factory)
            : factory;
}
