using System.Diagnostics.Metrics;
using ActualLab.Fusion.Diagnostics;

namespace ActualLab.Fusion.Tests.Services;

/// <summary>
/// Records the "operation" tag of every <see cref="FusionInstruments.RemoteComputedCacheStaleValueCount"/>
/// measurement. The meter is process-global, so tests using it belong to the time-sensitive collection.
/// </summary>
public sealed class StaleValueListener : IDisposable
{
    private readonly MeterListener _listener;

    public ConcurrentQueue<string> Operations { get; } = new();

    public StaleValueListener()
    {
        var staleValueCount = FusionInstruments.RemoteComputedCacheStaleValueCount;
        staleValueCount.Name.Should().Be("remote_computed.cache.stale_value.count");
        staleValueCount.Unit.Should().Be("{request}");
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, staleValueCount))
                meterListener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) => {
            value.Should().Be(1);
            tags.Length.Should().Be(1);
            Operations.Enqueue(GetTag(tags, "operation"));
        });
        _listener.Start();
    }

    public void Dispose()
        => _listener.Dispose();

    // Private methods

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }
}
