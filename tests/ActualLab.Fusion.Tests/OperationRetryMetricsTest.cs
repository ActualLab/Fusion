using System.Diagnostics.Metrics;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

// Meters are process-global, so concurrently running tests distort the measurements
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class OperationRetryMetricsTest : FusionTestBase
{
    public OperationRetryMetricsTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.Sqlite;

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (!isClient)
            services.AddFusion().AddService<ReprocessTester>();
    }

    [Fact]
    public async Task RetryCountAndDelayTest()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        var retryCount = FusionInstruments.OperationRetryCount;
        var retryDelay = FusionInstruments.OperationRetryDelay;
        retryCount.Name.Should().Be("operation.retry.count");
        retryCount.Unit.Should().Be("{retry}");
        retryDelay.Name.Should().Be("operation.retry.delay");
        retryDelay.Unit.Should().Be("ms");
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, retryCount)
                || ReferenceEquals(instrument, retryDelay))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(
                instrument.Name,
                value,
                GetTag(tags, "command.name"),
                GetTag(tags, "transiency"),
                GetTag(tags, "outcome"),
                tags.Length)));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(
                instrument.Name,
                value,
                GetTag(tags, "command.name"),
                GetTag(tags, "transiency"),
                GetTag(tags, "outcome"),
                tags.Length)));
        listener.Start();

        var tester = Services.GetRequiredService<ReprocessTester>();
        tester.FailBeforeCommit = 2;
        (await Services.Commander().Call(new ReprocessTester_Run())).Should().Be(3);

        var retryCounts = measurements
            .Where(x => x.InstrumentName == "operation.retry.count")
            .ToArray();
        retryCounts.Should().HaveCount(2);
        retryCounts.Should().OnlyContain(x => x.Value == 1);
        retryCounts.Should().OnlyContain(x => x.Outcome == "scheduled" && x.TagCount == 3);
        var retryDelays = measurements
            .Where(x => x.InstrumentName == "operation.retry.delay")
            .ToArray();
        retryDelays.Should().HaveCount(2);
        retryDelays.Should().OnlyContain(x => x.Value > 0);
        retryDelays.Should().OnlyContain(x =>
            x.CommandName == nameof(ReprocessTester_Run)
            && x.Transiency == "transient"
            && x.TagCount == 2);
    }

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }

    private sealed record Measurement(
        string InstrumentName,
        double Value,
        string CommandName,
        string Transiency,
        string Outcome,
        int TagCount);
}
