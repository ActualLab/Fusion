using System.Diagnostics;
using ActualLab.CommandR.Diagnostics;
using ActualLab.Diagnostics;
using ActualLab.Reflection;
using ActualLab.Rpc;

namespace ActualLab.Tests.CommandR;

public sealed class CommandActivityPayloadTest
{
    [Fact]
    public async Task PayloadCaptureTest()
    {
        var defaultCommand = new PayloadCommand("default");
        var defaultActivity = await Run(defaultCommand, null, ActivitySamplingResult.AllDataAndRecorded);
        var commandName = typeof(PayloadCommand).GetName();
        defaultCommand.FormatCount.Should().Be(0);
        defaultActivity.OperationName.Should().Be($"cmd.{DiagnosticsExt.FixName(commandName)}");
        defaultActivity.GetTagItem("command.name").Should().Be(commandName);
        defaultActivity.GetTagItem("command.kind").Should().Be("command");
        defaultActivity.GetTagItem("command.scope").Should().Be("outermost");
        GetEventTag(defaultActivity, "command.payload").Should().BeNull();

        var propagationCommand = new PayloadCommand("propagation");
        var propagationActivity = await Run(
            propagationCommand,
            new CommandTracer.Options() { CaptureCommandPayload = true },
            ActivitySamplingResult.PropagationData);
        propagationActivity.IsAllDataRequested.Should().BeFalse();
        propagationCommand.FormatCount.Should().Be(0);
        GetEventTag(propagationActivity, "command.payload").Should().BeNull();

        var capturedCommand = new PayloadCommand("captured");
        var capturedActivity = await Run(
            capturedCommand,
            new CommandTracer.Options() { CaptureCommandPayload = true },
            ActivitySamplingResult.AllDataAndRecorded);
        capturedCommand.FormatCount.Should().Be(1);
        GetEventTag(capturedActivity, "command.payload").Should().Be("captured");

        var throwingCommand = new PayloadCommand("unused") { MustThrow = true };
        var throwingActivity = await Run(
            throwingCommand,
            new CommandTracer.Options() { CaptureCommandPayload = true },
            ActivitySamplingResult.AllDataAndRecorded);
        throwingCommand.FormatCount.Should().Be(1);
        throwingActivity.Events.Should().Contain(x => x.Name == "command.payload.error");
    }

    private static async Task<Activity> Run(
        PayloadCommand command,
        CommandTracer.Options? settings,
        ActivitySamplingResult samplingResult)
    {
        Activity? completedActivity = null;
        var activitySourceName = CommanderInstruments.ActivitySource.Name;
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => samplingResult,
            ActivityStopped = activity => completedActivity = activity,
        };
        ActivitySource.AddActivityListener(listener);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddRpc();
        var commander = serviceCollection.AddCommander();
        serviceCollection.AddSingleton<PayloadCommandHandler>();
        commander.AddHandlers<PayloadCommandHandler>();
        if (settings is not null)
            serviceCollection.AddSingleton(settings);
        await using var services = serviceCollection.BuildServiceProvider();

        await services.Commander().Call(command);
        completedActivity.Should().NotBeNull();
        return completedActivity!;
    }

    private static object? GetEventTag(Activity activity, string name)
        => activity.Events
            .SelectMany(x => x.Tags)
            .FirstOrDefault(x => x.Key == name)
            .Value;

    private sealed class PayloadCommand(string payload) : ICommand<Unit>
    {
        private int _formatCount;

        public int FormatCount => _formatCount;
        public bool MustThrow { get; init; }

        public override string ToString()
        {
            Interlocked.Increment(ref _formatCount);
            return MustThrow ? throw new InvalidOperationException("Formatting failed.") : payload;
        }
    }

    private sealed class PayloadCommandHandler : ICommandHandler<PayloadCommand>
    {
        public Task OnCommand(
            PayloadCommand command,
            CommandContext context,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
