using System.Diagnostics.Metrics;
using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcCallTrackerMetricsTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
        services.AddSingleton(_ => RpcDiagnosticsOptions.Default with {
            OpenCallMetricsPeriodProvider = _ => TimeSpan.FromMilliseconds(20),
        });
        services.AddSingleton(_ => new RpcLimits(useDebugDefaults: false) {
            CallTimeoutCheckPeriod = TimeSpan.FromMilliseconds(20),
        });
    }

    [Fact]
    public async Task ReportsOpenCallsAndBatchedEvents()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.OpenInboundCallGauge)
                || ReferenceEquals(instrument, RpcInstruments.OpenOutboundCallGauge)
                || ReferenceEquals(instrument, RpcInstruments.ClientCallEventCounter))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        using var cancellationSource = new CancellationTokenSource();
        var callTask = client.Delay(TimeSpan.FromSeconds(10), cancellationSource.Token);
        await WaitFor(() =>
            connection.ClientPeer.OutboundCalls.Count > 0
            && connection.ServerPeer.InboundCalls.Count > 0);

        await WaitFor(() => {
            listener.RecordObservableInstruments();
            return GetStageValue(measurements, RpcInstruments.OpenOutboundCallGauge.Name, "pending") > 0
                && GetStageValue(measurements, RpcInstruments.OpenInboundCallGauge.Name, "pending") > 0;
        });

        cancellationSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);
        await WaitFor(() =>
            connection.ClientPeer.OutboundCalls.Count == 0
            && connection.ServerPeer.InboundCalls.Count == 0);
        await WaitFor(() => {
            listener.RecordObservableInstruments();
            return GetStageValue(measurements, RpcInstruments.OpenOutboundCallGauge.Name, "pending") == 0
                && GetStageValue(measurements, RpcInstruments.OpenInboundCallGauge.Name, "pending") == 0;
        });

        RpcInstruments.RegisterClientCallEvents(delayedCount: 2, resendCount: 3, timeoutCount: 4);

        RpcInstruments.OpenInboundCallGauge.Name.Should().Be("rpc.server.call.open");
        RpcInstruments.OpenInboundCallGauge.Unit.Should().Be("{call}");
        RpcInstruments.OpenOutboundCallGauge.Name.Should().Be("rpc.client.call.open");
        RpcInstruments.OpenOutboundCallGauge.Unit.Should().Be("{call}");
        RpcInstruments.ClientCallEventCounter.Unit.Should().Be("{event}");
        measurements.Should().Contain(x => HasStage(x.Tags, "pending"));
        measurements.Should().Contain(x => HasStage(x.Tags, "result_ready"));
        measurements.Should().Contain(x => HasStage(x.Tags, "invalidated"));
        measurements.Should().Contain(x => x.Value == 2 && HasEvent(x.Tags, "delayed"));
        measurements.Should().Contain(x => x.Value == 3 && HasEvent(x.Tags, "resend"));
        measurements.Should().Contain(x => x.Value == 4 && HasEvent(x.Tags, "timeout"));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) {
            if (condition.Invoke())
                return;

            await Task.Delay(10);
        }
        Assert.Fail("The expected RPC call tracker state wasn't reached.");
    }

    private static bool HasEvent(KeyValuePair<string, object?>[] tags, string value)
        => tags.Any(x => x.Key == "rpc.call.event" && Equals(x.Value, value));

    private static bool HasStage(KeyValuePair<string, object?>[] tags, string value)
        => tags.Any(x => x.Key == "rpc.call.stage" && Equals(x.Value, value));

    private static long GetStageValue(
        IEnumerable<Measurement> measurements,
        string instrumentName,
        string stage)
        => measurements.Last(x => x.Name == instrumentName && HasStage(x.Tags, stage)).Value;

    private sealed record Measurement(
        string Name,
        long Value,
        KeyValuePair<string, object?>[] Tags);
}
