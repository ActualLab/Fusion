namespace ActualLab.Compliance;

/// <summary>
/// A logger factory that wraps every logger it creates into a <see cref="SanitizingLogger"/>.
/// Wrapping the factory is cheaper than wrapping each <see cref="ILoggerProvider"/>, and it
/// covers providers registered later.
/// </summary>
public sealed class SanitizingLoggerFactory(ILoggerFactory innerFactory, bool mustSanitize = true)
    : ILoggerFactory
{
    public ILoggerFactory InnerFactory { get; } = innerFactory;
    public bool MustSanitize { get; } = mustSanitize;

    public ILogger CreateLogger(string categoryName)
    {
        var logger = InnerFactory.CreateLogger(categoryName);
        return MustSanitize
            ? new SanitizingLogger(logger)
            : logger;
    }

    public void AddProvider(ILoggerProvider provider)
        => InnerFactory.AddProvider(provider);

    public void Dispose()
        => InnerFactory.Dispose();
}
