using System.Diagnostics.Metrics;
using ActualLab.Fusion.EntityFramework.Internal;

namespace ActualLab.Tests.Diagnostics;

public sealed class FusionEntityFrameworkInstrumentsTest
{
    [Fact]
    public void MetricsHaveStableContractsAndTags()
    {
        FusionEntityFrameworkInstruments.OperationLogProcessingDelay.Name
            .Should().Be("db.operation_log.processing.delay");
        FusionEntityFrameworkInstruments.OperationLogProcessingDelay.Unit.Should().Be("ms");
        FusionEntityFrameworkInstruments.EventLogProcessingDelay.Name
            .Should().Be("db.event_log.processing.delay");
        FusionEntityFrameworkInstruments.EventLogProcessingDelay.Unit.Should().Be("ms");
        FusionEntityFrameworkInstruments.LogBatchSize.Name.Should().Be("db.log.batch.size");
        FusionEntityFrameworkInstruments.LogBatchSize.Unit.Should().Be("{entry}");
        FusionEntityFrameworkInstruments.LogBatchDuration.Name.Should().Be("db.log.batch.duration");
        FusionEntityFrameworkInstruments.LogBatchDuration.Unit.Should().Be("ms");

        var measurements = new ConcurrentQueue<Measurement>();
        using var listener = new MeterListener {
            InstrumentPublished = (instrument, meterListener) => {
                if (instrument.Meter == FusionEntityFrameworkInstruments.Meter)
                    meterListener.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        FusionEntityFrameworkInstruments.OperationLogProcessingDelay.Record(11,
            new KeyValuePair<string, object?>("shard", "test-shard"),
            new KeyValuePair<string, object?>("path", "gap"));
        FusionEntityFrameworkInstruments.EventLogProcessingDelay.Record(12,
            new KeyValuePair<string, object?>("shard", "test-shard"),
            new KeyValuePair<string, object?>("path", "reprocess"));
        FusionEntityFrameworkInstruments.LogBatchSize.Record(13,
            new KeyValuePair<string, object?>("log.kind", "operation"),
            new KeyValuePair<string, object?>("outcome", "success"));
        FusionEntityFrameworkInstruments.LogBatchDuration.Record(14,
            new KeyValuePair<string, object?>("log.kind", "event"),
            new KeyValuePair<string, object?>("outcome", "error"));

        AssertMeasurement("db.operation_log.processing.delay", 11, "shard", "test-shard", "path", "gap");
        AssertMeasurement("db.event_log.processing.delay", 12, "shard", "test-shard", "path", "reprocess");
        AssertMeasurement("db.log.batch.size", 13, "log.kind", "operation", "outcome", "success");
        AssertMeasurement("db.log.batch.duration", 14, "log.kind", "event", "outcome", "error");
        return;

        void AssertMeasurement(
            string name, double value, string firstTag, string firstValue, string secondTag, string secondValue)
        {
            var measurement = measurements.Single(x => x.Name == name && x.Value == value);
            measurement.Tags.Should().Equal(
                new KeyValuePair<string, object?>(firstTag, firstValue),
                new KeyValuePair<string, object?>(secondTag, secondValue));
        }
    }

    // Nested types

    private sealed record Measurement(
        string Name,
        double Value,
        KeyValuePair<string, object?>[] Tags);
}
