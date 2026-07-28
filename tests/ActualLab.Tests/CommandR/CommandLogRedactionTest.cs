using System.Diagnostics;
using ActualLab.CommandR.Diagnostics;
using ActualLab.Reflection;
using ActualLab.Rpc;

namespace ActualLab.Tests.CommandR;

[Collection(nameof(CommanderActivityTests))]
public sealed class CommandLogRedactionTest
{
    private const string Secret = "s-1234567890-must-never-be-logged";

    [Fact]
    public async Task FailedCommandLoggingTest()
    {
        var loggedCommand = new SecretCommand();
        var loggedRecords = await Run(loggedCommand);
        loggedRecords.Should().Contain(x => x.Contains(Secret, StringComparison.Ordinal));

        var notLoggedCommand = new SecretNotLoggedCommand();
        var notLoggedRecords = await Run(notLoggedCommand);
        notLoggedRecords.Should().NotContain(x => x.Contains(Secret, StringComparison.Ordinal));
        notLoggedRecords.Should().Contain(x => x.Contains("command failed", StringComparison.Ordinal)
            && x.Contains(typeof(SecretNotLoggedCommand).GetName(), StringComparison.Ordinal));
        notLoggedCommand.FormatCount.Should().Be(0);
    }

    // Private methods

    private static async Task<List<string>> Run(ICommand<Unit> command)
    {
        var records = new List<string>();
        var activitySourceName = CommanderInstruments.ActivitySource.Name;
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddRpc();
        serviceCollection.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(new RecordingLoggerProvider(records));
        });
        var commander = serviceCollection.AddCommander();
        serviceCollection.AddSingleton<FailingCommandHandler>();
        commander.AddHandlers<FailingCommandHandler>();
        await using var services = serviceCollection.BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(command));

        lock (records)
            return records.ToList();
    }

    // Nested types

    private sealed class SecretCommand : ICommand<Unit>
    {
        public override string ToString()
            => Secret;
    }

    private sealed class SecretNotLoggedCommand : ICommand<Unit>, INotLogged
    {
        private int _formatCount;
        public int FormatCount => _formatCount;

        public override string ToString()
        {
            Interlocked.Increment(ref _formatCount);
            return Secret;
        }
    }

    private sealed class FailingCommandHandler
        : ICommandHandler<SecretCommand>, ICommandHandler<SecretNotLoggedCommand>
    {
        public Task OnCommand(SecretCommand command, CommandContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Intended.");

        public Task OnCommand(
            SecretNotLoggedCommand command, CommandContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Intended.");
    }

    private sealed class RecordingLoggerProvider(List<string> records) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(records);

        public void Dispose()
        { }

        private sealed class RecordingLogger(List<string> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (records)
                    records.Add(formatter(state, exception));
            }
        }
    }
}
