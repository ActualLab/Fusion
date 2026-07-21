using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Diagnostics;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Reflection;

namespace ActualLab.Fusion.Tests.Internal;

public sealed class InvalidationDiagnosticsTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task PayloadCaptureTest()
    {
        var defaultCommand = new InvalidationDiagnosticsCommand("default");
        var defaultActivity = await Run(defaultCommand, false, ActivitySamplingResult.AllDataAndRecorded);
        var commandName = typeof(InvalidationDiagnosticsCommand).GetName();
        defaultCommand.FormatCount.Should().Be(0);
        defaultActivity.OperationName.Should().Be($"-inv.{DiagnosticsExt.FixName(commandName)}");
        defaultActivity.GetTagItem("command.name").Should().Be(commandName);
        GetEventTag(defaultActivity, "command.payload").Should().BeNull();

        var propagationCommand = new InvalidationDiagnosticsCommand("propagation");
        var propagationActivity = await Run(propagationCommand, true, ActivitySamplingResult.PropagationData);
        propagationActivity.IsAllDataRequested.Should().BeFalse();
        propagationCommand.FormatCount.Should().Be(0);
        GetEventTag(propagationActivity, "command.payload").Should().BeNull();

        var capturedCommand = new InvalidationDiagnosticsCommand("captured");
        var capturedActivity = await Run(capturedCommand, true, ActivitySamplingResult.AllDataAndRecorded);
        capturedCommand.FormatCount.Should().Be(1);
        GetEventTag(capturedActivity, "command.payload").Should().Be("captured");

        var throwingCommand = new InvalidationDiagnosticsCommand("unused") { MustThrowOnFormat = true };
        var throwingActivity = await Run(throwingCommand, true, ActivitySamplingResult.AllDataAndRecorded);
        throwingCommand.FormatCount.Should().Be(1);
        throwingActivity.Events.Should().Contain(x => x.Name == "command.payload.error");
    }

    [Fact]
    public async Task FailureAndPassMetricsTest()
    {
        var activities = new ConcurrentQueue<Activity>();
        var activitySourceName = FusionInstruments.ActivitySource.Name;
        var commandName = typeof(InvalidationDiagnosticsCommand).GetName();
        using var activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem("command.name"), commandName))
                    activities.Enqueue(activity);
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new ConcurrentQueue<Measurement>();
        var duration = FusionInstruments.InvalidationPassDuration;
        var commandCount = FusionInstruments.InvalidationPassCommandCount;
        duration.Name.Should().Be("invalidation.pass.duration");
        duration.Unit.Should().Be("ms");
        commandCount.Name.Should().Be("invalidation.pass.command.count");
        commandCount.Unit.Should().Be("{command}");
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) => {
            if (ReferenceEquals(instrument, duration) || ReferenceEquals(instrument, commandCount))
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        meterListener.Start();

        using var services = CreateDiagnosticServices(false);
        await services.Commander().Call(new InvalidationDiagnosticsCommand("success"));
        await services.Commander().Call(new InvalidationDiagnosticsCommand("failure") { MustFail = true });

        activities.Should().HaveCount(2);
        var failedActivity = activities.Single(x => x.Status == ActivityStatusCode.Error);
        failedActivity.GetTagItem("invalidation.partial_failure").Should().Be(true);
        failedActivity.GetTagItem("invalidation.failure.count").Should().Be(1);
        failedActivity.Events.Should().Contain(x =>
            x.Name == "exception"
            && x.Tags.Any(t => t.Key == "exception.type"));

        var ownMeasurements = measurements.Where(x => x.CommandName == commandName).ToArray();
        var durations = ownMeasurements.Where(x => x.InstrumentName == duration.Name).ToArray();
        durations.Should().HaveCount(2);
        durations.Should().OnlyContain(x => x.Value >= 0);
        durations.Select(x => x.Outcome).Should().BeEquivalentTo("success", "error");
        var counts = ownMeasurements.Where(x => x.InstrumentName == commandCount.Name).ToArray();
        counts.Should().HaveCount(2);
        counts.Should().OnlyContain(x => x.Value == 1);
        counts.Select(x => x.Outcome).Should().BeEquivalentTo("success", "error");
        ownMeasurements.Should().OnlyContain(x => x.TagCount == 2);
    }

    private async Task<Activity> Run(
        InvalidationDiagnosticsCommand command,
        bool captureCommandPayload,
        ActivitySamplingResult samplingResult)
    {
        Activity? completedActivity = null;
        var activitySourceName = FusionInstruments.ActivitySource.Name;
        var commandName = typeof(InvalidationDiagnosticsCommand).GetName();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => samplingResult,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem("command.name"), commandName))
                    completedActivity = activity;
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var services = CreateDiagnosticServices(captureCommandPayload);

        await services.Commander().Call(command);
        completedActivity.Should().NotBeNull();
        return completedActivity!;
    }

    private ServiceProvider CreateDiagnosticServices(bool captureCommandPayload)
        => CreateServices(services => {
            services.AddFusion().AddService<IInvalidationDiagnosticsService, InvalidationDiagnosticsService>();
            services.AddSingleton(new InvalidatingCommandCompletionHandler.Options() {
                LogLevel = LogLevel.None,
                CaptureCommandPayload = captureCommandPayload,
            });
            services.AddSingleton(new CompletionProducer.Options() { LogLevel = LogLevel.None });
        });

    private static Measurement NewMeasurement(
        string instrumentName,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => new(
            instrumentName,
            value,
            GetTag(tags, "command.name"),
            GetTag(tags, "outcome"),
            tags.Length);

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }

    private static object? GetEventTag(Activity activity, string name)
        => activity.Events
            .SelectMany(x => x.Tags)
            .FirstOrDefault(x => x.Key == name)
            .Value;

    public interface IInvalidationDiagnosticsService : IComputeService
    {
        [ComputeMethod]
        Task<int> Get(CancellationToken cancellationToken = default);

        [CommandHandler]
        Task OnRun(InvalidationDiagnosticsCommand command, CancellationToken cancellationToken = default);
    }

    public class InvalidationDiagnosticsService : IInvalidationDiagnosticsService
    {
        public virtual Task<int> Get(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public virtual Task OnRun(
            InvalidationDiagnosticsCommand command,
            CancellationToken cancellationToken = default)
        {
            if (Invalidation.IsActive) {
                if (command.MustFail)
                    throw new InvalidOperationException("Invalidation failed.");

                _ = Get(default);
                return Task.CompletedTask;
            }

            InMemoryOperationScope.Require();
            return Task.CompletedTask;
        }
    }

    public sealed class InvalidationDiagnosticsCommand(string payload) : ICommand<Unit>
    {
        private int _formatCount;

        public int FormatCount => _formatCount;
        public bool MustFail { get; init; }
        public bool MustThrowOnFormat { get; init; }

        public override string ToString()
        {
            Interlocked.Increment(ref _formatCount);
            return MustThrowOnFormat ? throw new InvalidOperationException("Formatting failed.") : payload;
        }
    }

    private sealed record Measurement(
        string InstrumentName,
        double Value,
        string CommandName,
        string Outcome,
        int TagCount);
}
