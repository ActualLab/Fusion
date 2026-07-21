using System.Diagnostics.Metrics;
using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcConnectionMetricsTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task ReportsConnectionLifecycleMetrics()
    {
        var counts = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        var measurements = new ConcurrentQueue<(
            string Name,
            double Value,
            KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.ConnectionAttemptCounter)
                || ReferenceEquals(instrument, RpcInstruments.ConnectionAttemptDurationHistogram)
                || ReferenceEquals(instrument, RpcInstruments.ConnectionUptimeHistogram))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            value.Should().Be(1);
            counts.Enqueue(tags.ToArray());
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue((instrument.Name, value, tags.ToArray())));
        listener.Start();

        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        (await client.Add(1, 2)).Should().Be(3);
        await Delay(0.02);
        await connection.Disconnect();
        await connection.Connect();
        (await client.Add(2, 3)).Should().Be(5);
        await AssertNoCalls(connection.ClientPeer, Out);

        RpcInstruments.ConnectionAttemptCounter.Name.Should().Be("rpc.connection.attempt.count");
        RpcInstruments.ConnectionAttemptCounter.Unit.Should().Be("{attempt}");
        RpcInstruments.ConnectionAttemptDurationHistogram.Name.Should().Be("rpc.connection.attempt.duration");
        RpcInstruments.ConnectionAttemptDurationHistogram.Unit.Should().Be("ms");
        RpcInstruments.ConnectionUptimeHistogram.Name.Should().Be("rpc.connection.uptime");
        RpcInstruments.ConnectionUptimeHistogram.Unit.Should().Be("ms");

        counts.Count.Should().BeGreaterThanOrEqualTo(4);
        counts.Should().Contain(tags => HasTag(tags, "rpc.peer.type", "client"));
        counts.Should().Contain(tags => HasTag(tags, "rpc.peer.type", "server"));
        counts.Should().Contain(tags => HasTag(tags, "outcome", "success"));
        counts.Should().OnlyContain(tags => HasStableConnectionTags(tags));

        var attemptDurations = measurements
            .Where(x => x.Name == RpcInstruments.ConnectionAttemptDurationHistogram.Name)
            .ToArray();
        attemptDurations.Should().HaveCountGreaterThanOrEqualTo(4);
        attemptDurations.Should().OnlyContain(x => x.Value >= 0 && HasStableConnectionTags(x.Tags));

        var uptimes = measurements
            .Where(x => x.Name == RpcInstruments.ConnectionUptimeHistogram.Name)
            .ToArray();
        uptimes.Should().HaveCountGreaterThanOrEqualTo(2);
        uptimes.Should().OnlyContain(x => x.Value >= 0 && HasStableConnectionTags(x.Tags));
    }

    private static bool HasStableConnectionTags(KeyValuePair<string, object?>[] tags)
    {
        var expectedKeys = new[] { "rpc.peer.type", "rpc.connection.kind", "outcome" };
        return tags.Select(x => x.Key).Order().SequenceEqual(expectedKeys.Order())
            && tags.Any(x => x is { Key: "rpc.peer.type", Value: "client" or "server" })
            && HasTag(tags, "rpc.connection.kind", "remote")
            && tags.Any(x => x is { Key: "outcome", Value: "success" or "error" or "cancel" });
    }

    private static bool HasTag(
        IEnumerable<KeyValuePair<string, object?>> tags,
        string key,
        string value)
        => tags.Any(x => x.Key == key && Equals(x.Value, value));
}
