using System.Diagnostics.Metrics;
using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

// Meters are process-global, so concurrently running tests distort the measurements
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RemoteComputedCacheMetricsTest : FusionTestBase
{
    public RemoteComputedCacheMetricsTest(ITestOutputHelper @out) : base(@out)
        => UseRemoteComputedCache = true;

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient)
            fusion.AddService<IKeyValueService<string>, KeyValueService<string>>();
        else
            fusion.AddClient<IKeyValueService<string>>();
    }

    [Fact]
    public async Task RequestCountAndLookupDurationTest()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        var requestCount = FusionInstruments.RemoteComputedCacheRequestCount;
        var lookupDuration = FusionInstruments.RemoteComputedCacheLookupDuration;
        requestCount.Name.Should().Be("remote_computed.cache.request.count");
        requestCount.Unit.Should().Be("{request}");
        lookupDuration.Name.Should().Be("remote_computed.cache.lookup.duration");
        lookupDuration.Unit.Should().Be("ms");
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, requestCount) || ReferenceEquals(instrument, lookupDuration))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        listener.Start();

        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;
        var service = WebServices.GetRequiredService<IKeyValueService<string>>();
        var client1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var client2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await service.Set("1", "a");
        var computed1 = await GetComputed(client1, "1");
        computed1.Value.Should().Be("a");
        var computed2 = await GetComputed(client2, "1");
        computed2.Value.Should().Be("a");
        await computed2.WhenSynchronized.WaitAsync(TimeSpan.FromSeconds(5));

        var requests = measurements.Where(x => x.InstrumentName == requestCount.Name).ToArray();
        requests.Should().HaveCount(2);
        requests.Should().OnlyContain(x => x.Value == 1 && x.TagCount == 1);
        requests.Select(x => x.Outcome).Should().BeEquivalentTo("miss", "hit");
        var durations = measurements.Where(x => x.InstrumentName == lookupDuration.Name).ToArray();
        durations.Should().HaveCount(2);
        durations.Should().OnlyContain(x => x.Value >= 0 && x.TagCount == 1);
        durations.Select(x => x.Outcome).Should().BeEquivalentTo("miss", "hit");
    }

    private static async Task<RemoteComputed<string>> GetComputed(IKeyValueService<string> service, string key)
        => (RemoteComputed<string>)await Computed.Capture(() => service.Get(key));

    private static Measurement NewMeasurement(
        string instrumentName,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => new(instrumentName, value, GetTag(tags, "outcome"), tags.Length);

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }

    private sealed record Measurement(string InstrumentName, double Value, string Outcome, int TagCount);
}
