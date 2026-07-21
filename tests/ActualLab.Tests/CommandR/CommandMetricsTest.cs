using System.Diagnostics.Metrics;
using ActualLab.CommandR.Diagnostics;
using ActualLab.Tests.CommandR.Services;

namespace ActualLab.Tests.CommandR;

public class CommandMetricsTest(ITestOutputHelper @out) : CommandRTestBase(@out)
{
    [Fact]
    public async Task ExecutionDurationTest()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        var duration = CommanderInstruments.CommandExecutionDuration;
        duration.Name.Should().Be("command.execution.duration");
        duration.Unit.Should().Be("ms");
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, duration))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            measurements.Enqueue(new Measurement(
                value,
                GetTag(tags, "command.name"),
                GetTag(tags, "command.kind"),
                GetTag(tags, "command.scope"),
                GetTag(tags, "outcome"),
                tags.Length)));
        listener.Start();

        var services = CreateServices();
        await services.Commander().Call(new LogCommand() { Message = "success" });
        await Assert.ThrowsAsync<DivideByZeroException>(() =>
            services.Commander().Call(new DivCommand() { Divisible = 1, Divisor = 0 }));
        using (var cancellationSource = new CancellationTokenSource(20)) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                services.Commander().Call(new IncSetFailCommand() {
                    SetDelay = 10_000,
                    IncrementDelay = 10_000,
                    FailDelay = 10_000,
                }, cancellationSource.Token));
        }
        await services.Commander().Call(new RecSumCommand() { Arguments = [1, 2] });

        measurements.Should().Contain(x =>
            x.CommandName == nameof(LogCommand)
            && x.CommandKind == "command"
            && x.CommandScope == "outermost"
            && x.Outcome == "success");
        measurements.Should().Contain(x =>
            x.CommandName == nameof(DivCommand)
            && x.CommandKind == "command"
            && x.CommandScope == "outermost"
            && x.Outcome == "error");
        measurements.Should().Contain(x =>
            x.CommandName == nameof(IncSetFailCommand)
            && x.CommandKind == "event"
            && x.CommandScope == "outermost"
            && x.Outcome == "cancel");
        measurements.Should().Contain(x =>
            x.CommandName == nameof(RecSumCommand)
            && x.CommandKind == "command"
            && x.CommandScope == "nested"
            && x.Outcome == "success");
        measurements.Should().OnlyContain(x => x.DurationMs >= 0);
        measurements.Should().OnlyContain(x => x.TagCount == 4);
    }

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }

    private sealed record Measurement(
        double DurationMs,
        string CommandName,
        string CommandKind,
        string CommandScope,
        string Outcome,
        int TagCount);
}
