using ActualLab.Compliance;

namespace ActualLab.Tests.Compliance;

public class SanitizingLoggerTest
{
    // 11 chars, so PrefixAndLengthHint renders the 8-15 bucket
    private const string Secret = "Hello world";

    [Fact]
    public void SensitiveArgIsMasked()
    {
        var (loggerFactory, messages) = CreateLoggerFactory(useSanitizing: true);
        var log = loggerFactory.CreateLogger("Test");

        log.LogInformation("Text: {Text}", Private(Secret));

        messages.Should().ContainSingle()
            .Which.Should().Contain("<<He* [8-15]>>");
    }

    [Fact]
    public void SensitiveArgPassesThroughWithoutTheDecorator()
    {
        // The whole point of the default: nothing masks unless the logger asks for it
        var (loggerFactory, messages) = CreateLoggerFactory(useSanitizing: false);
        var log = loggerFactory.CreateLogger("Test");

        log.LogInformation("Text: {Text}", Private(Secret));

        messages.Should().ContainSingle()
            .Which.Should().Contain(Secret);
    }

    [Fact]
    public void NonSensitiveArgIsUnchanged()
    {
        var (loggerFactory, messages) = CreateLoggerFactory(useSanitizing: true);
        var log = loggerFactory.CreateLogger("Test");

        log.LogInformation("Id: {Id}", 42);

        messages.Should().ContainSingle()
            .Which.Should().Contain("42");
    }

    [Fact]
    public void SanitizationIsNotActiveOutsideTheLogCall()
    {
        // The scope must not leak past Log(): a serializer running right after it would
        // otherwise write the masked form
        var (loggerFactory, _) = CreateLoggerFactory(useSanitizing: true);
        var log = loggerFactory.CreateLogger("Test");

        Sanitization.IsActive.Should().BeFalse();
        log.LogInformation("test");
        Sanitization.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AnEagerlySanitizedArgumentIsNotMasked()
    {
        // Documents the trap: MaybeSanitize runs before the scope opens, so it renders raw.
        // Only a deferred ToString() - SanitizedString<T> or an ISanitized - is masked.
        var (loggerFactory, messages) = CreateLoggerFactory(useSanitizing: true);
        var log = loggerFactory.CreateLogger("Test");

        log.LogInformation("Text: {Text}", Sanitizer.MaybeSanitize<Sanitizers.PrefixAndLengthHint>(Secret));

        messages.Should().ContainSingle()
            .Which.Should().Contain(Secret);
    }

    [Fact]
    public void IsEnabledDelegatesToInner()
    {
        var (loggerFactory, _) = CreateLoggerFactory(useSanitizing: true, minLevel: LogLevel.Warning);
        var log = loggerFactory.CreateLogger("Test");

        log.IsEnabled(LogLevel.Information).Should().BeFalse();
        log.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }

    [Fact]
    public void FactoryCreatesDistinctLoggers()
    {
        using var innerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new InMemoryLoggerProvider()));
        using var factory = new SanitizingLoggerFactory(innerFactory);

        factory.CreateLogger("Cat1").Should().NotBeSameAs(factory.CreateLogger("Cat2"));
    }

    [Fact]
    public void FactoryCanBeDisabled()
    {
        using var innerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new InMemoryLoggerProvider()));
        using var factory = new SanitizingLoggerFactory(innerFactory, mustSanitize: false);

        factory.CreateLogger("Test").Should().NotBeOfType<SanitizingLogger>();
    }

    [Fact]
    public void SanitizingWrapsOnceAndHonoursTheFlag()
    {
        using var innerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new InMemoryLoggerProvider()));

        innerFactory.Sanitizing(false).Should().BeSameAs(innerFactory);

        var wrapped = innerFactory.Sanitizing();
        wrapped.Should().BeOfType<SanitizingLoggerFactory>();
        // Wrapping twice would open a redundant scope per log call
        wrapped.Sanitizing().Should().BeSameAs(wrapped);
    }

    [Fact]
    public void AddSanitizingLoggerFactoryWiresItUpInDi()
    {
        var messages = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(new InMemoryLoggerProvider(messages));
            logging.AddSanitizingLoggerFactory();
        });
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Test")
            .LogInformation("Text: {Text}", Private(Secret));

        messages.Should().ContainSingle()
            .Which.Should().Contain("<<He* [8-15]>>").And.NotContain(Secret);
    }

    // Private methods

    private static SanitizedString<Sanitizers.PrefixAndLengthHint> Private(string value)
        => new(value);

    private static (ILoggerFactory Factory, List<string> Messages) CreateLoggerFactory(
        bool useSanitizing,
        LogLevel minLevel = LogLevel.Debug)
    {
        var messages = new List<string>();
        var innerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(minLevel)
            .AddProvider(new InMemoryLoggerProvider(messages, minLevel)));
        var factory = useSanitizing
            ? new SanitizingLoggerFactory(innerFactory)
            : innerFactory;
        return (factory, messages);
    }

    // Nested types

    private sealed class InMemoryLoggerProvider(
        List<string>? messages = null,
        LogLevel minLevel = LogLevel.Debug)
        : ILoggerProvider
    {
        private readonly List<string> _messages = messages ?? [];

        public ILogger CreateLogger(string categoryName)
            => new InMemoryLogger(_messages, minLevel);

        public void Dispose() { }
    }

    private sealed class InMemoryLogger(List<string> messages, LogLevel minLevel) : ILogger
    {
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            messages.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= minLevel;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;
    }
}
