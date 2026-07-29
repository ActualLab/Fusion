using Microsoft.Extensions.Logging;

namespace ActualLab.Compliance;

public static class SanitizingLoggerFactoryExt
{
    /// <summary>
    /// Replaces the <see cref="ILoggerFactory"/> registration with one that wraps every logger
    /// into a <see cref="SanitizingLogger"/>, so <see cref="ISanitized"/> values are masked in
    /// the log - and only there.
    /// </summary>
    public static ILoggingBuilder AddSanitizingLoggerFactory(this ILoggingBuilder logging)
        => logging.AddSanitizingLoggerFactory(static _ => true);

    /// <inheritdoc cref="AddSanitizingLoggerFactory(ILoggingBuilder)"/>
    public static ILoggingBuilder AddSanitizingLoggerFactory(
        this ILoggingBuilder logging,
        Func<IServiceProvider, bool> mustSanitizeResolver)
    {
        logging.Services.AddSingleton<ILoggerFactory>(c => {
            var innerFactory = ActivatorUtilities.CreateInstance<LoggerFactory>(c);
            return mustSanitizeResolver.Invoke(c)
                ? new SanitizingLoggerFactory(innerFactory)
                : innerFactory;
        });
        return logging;
    }
}
