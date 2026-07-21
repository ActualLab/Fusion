using System.Diagnostics.Metrics;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcBasicTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddService<ICounterService, CounterService>(RpcServiceMode.Distributed);
    }

    [Fact]
    public async Task DistributedTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
        await clientPeer.WhenConnected();

        var counters = services.GetRequiredService<ICounterService>();

        var c = Computed.GetExisting(() => counters.Get("a"));
        c.Should().BeNull();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await counters.Get("a");
        });

        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await Computed.Capture(() => counters.Get("a"));
        });
    }

    [Fact]
    public async Task ReportsResultReadyOpenCalls()
    {
        var measurements = new ConcurrentQueue<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.OpenInboundCallGauge)
                || ReferenceEquals(instrument, RpcInstruments.OpenOutboundCallGauge))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        await using var services = CreateServices(s => {
            s.AddFusion().AddServerAndClient<IReconnectTester, ReconnectTester>();
            s.AddSingleton(_ => RpcDiagnosticsOptions.Default with {
                OpenCallMetricsPeriodProvider = _ => TimeSpan.FromMilliseconds(20),
            });
            s.AddSingleton(_ => new RpcLimits(useDebugDefaults: false) {
                CallTimeoutCheckPeriod = TimeSpan.FromMilliseconds(20),
            });
        });
        var client = services.RpcHub().GetClient<IReconnectTester>();
        var computed = await Computed.Capture(() => client.Delay(20, 500)).AsTask();

        await WaitFor(() => {
            listener.RecordObservableInstruments();
            return GetStageValue(measurements, RpcInstruments.OpenOutboundCallGauge.Name, "result_ready") > 0
                && GetStageValue(measurements, RpcInstruments.OpenInboundCallGauge.Name, "result_ready") > 0;
        });

        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task PeerMonitorTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
        var monitor = new RpcPeerStateMonitor(services.RpcHub(), clientPeer.Ref);
        var state = monitor.State;
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));

        _ = clientPeer.Disconnect(new InvalidOperationException("Disconnected!"));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustDisconnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Disconnected).WaitAsync(TimeSpan.FromSeconds(5));

        await testClient[clientPeer.Ref].Connect();
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) {
            if (condition.Invoke())
                return;

            await Task.Delay(10);
        }
        Assert.Fail("The expected RPC call metrics weren't reported.");
    }

    private static long GetStageValue(
        IEnumerable<Measurement> measurements,
        string instrumentName,
        string stage)
        => measurements.Last(x =>
            x.Name == instrumentName
            && x.Tags.Any(t => t.Key == "rpc.call.stage" && Equals(t.Value, stage))).Value;

    private sealed record Measurement(
        string Name,
        long Value,
        KeyValuePair<string, object?>[] Tags);
}
