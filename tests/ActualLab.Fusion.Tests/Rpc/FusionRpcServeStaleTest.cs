using System.Diagnostics.Metrics;
using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcServeStaleTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private volatile int _inboundCallDelayMs;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IServeStaleTester, ServeStaleTester>();
        services.AddRpc().AddMiddleware(_ => new RpcInboundCallDelayer() {
            DelayProvider = _ => TimeSpan.FromMilliseconds(_inboundCallDelayMs),
        });
        services.AddSingleton<IRemoteComputedCache>(
            c => new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c));
    }

    [Fact]
    public async Task SupersededStaleComputedMustSynchronizeTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        var timeout = TimeSpan.FromSeconds(5);

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        c1.Value.Should().Be("v-1");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update(); // Disconnected + cached value -> serve-stale
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();

        c2.Invalidate();
        var c3 = (RemoteComputed<string>)await c2.Update(); // Serve-stale again, c2 is superseded
        c3.WhenSynchronized.IsCompleted.Should().BeFalse();

        await connection.Connect();
        await c3.WhenInvalidated().WaitAsync(timeout); // InvalidateWhenReconnected
        var c4 = (RemoteComputed<string>)await c3.Update(); // Real RPC call
        c4.IsConsistent().Should().BeTrue();
        await c4.WhenSynchronized.WaitAsync(timeout);
        await c3.WhenSynchronized.WaitAsync(timeout);

        // Every superseded computed must synchronize once its successor does -
        // otherwise ComputedSynchronizer.Precise waits on it forever
        await c2.WhenSynchronized.WaitAsync(timeout);
        operations.Should().BeEquivalentTo("connection_check", "connection_check");
    }

    [Fact]
    public async Task MidCallDisconnectStaleComputedMustSynchronizeTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        var timeout = TimeSpan.FromSeconds(5);

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update(); // Disconnected + cached value -> serve-stale
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();

        await connection.Connect();
        await c2.WhenInvalidated().WaitAsync(timeout); // InvalidateWhenReconnected
        _inboundCallDelayMs = 1000;
        var updateTask = c2.Update();
        await Delay(0.3);
        await connection.Disconnect(); // Mid-call disconnect -> the send/disconnect race branch
        var c3 = (RemoteComputed<string>)await updateTask;
        c3.WhenSynchronized.IsCompleted.Should().BeFalse();

        _inboundCallDelayMs = 0;
        await connection.Connect();
        await c3.WhenInvalidated().WaitAsync(timeout);
        var c4 = (RemoteComputed<string>)await c3.Update();
        c4.IsConsistent().Should().BeTrue();
        await c3.WhenSynchronized.WaitAsync(timeout);

        // Every superseded computed must synchronize once its successor does -
        // otherwise ComputedSynchronizer.Precise waits on it forever
        await c2.WhenSynchronized.WaitAsync(timeout);
        operations.Should().BeEquivalentTo("connection_check", "active_call");
    }

    private static MeterListener StartStaleValueListener(ConcurrentQueue<string> operations)
    {
        var staleValueCount = FusionInstruments.RemoteComputedCacheStaleValueCount;
        staleValueCount.Name.Should().Be("remote_computed.cache.stale_value.count");
        staleValueCount.Unit.Should().Be("{request}");
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, staleValueCount))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => {
            value.Should().Be(1);
            tags.Length.Should().Be(1);
            operations.Enqueue(GetTag(tags, "operation"));
        });
        listener.Start();
        return listener;
    }

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }
}
