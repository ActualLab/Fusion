using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcCancellationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddService<ICounterService, CounterService>(RpcServiceMode.HybridServer);
    }

    [Fact]
    public async Task CancelTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<ICounterService>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()
            => await Computed.Capture(() => counters.Get("cancel")));

        using var cts = new CancellationTokenSource(100);
        // ReSharper disable once MethodSupportsCancellation
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()
            => await Computed.Capture(() => counters.Get("wait", cts.Token)));
    }

    [Fact]
    public async Task ReprocessCancelTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<ICounterService>();
        var key = "cancel50";
        var timeout = TimeSpan.FromSeconds(1);

        // ReSharper disable once MethodSupportsCancellation
        var c = await Computed.Capture(() => counters.Get(key));
        c.Value.Should().Be(0);

        // Test w/o CancellationToken
        await Test(CancellationToken.None);
        await counters.Set(key, 0); // Post-test reset
        await Task.Delay(500);

        // Test with CancellationToken
        using var cts = new CancellationTokenSource(60_000);
        await Test(cts.Token);

        async Task Test(CancellationToken cancellationToken) {
            c = await Computed.Capture(() => counters.Get(key, cancellationToken))
                .AsTask().WaitAsync(timeout);
            for (var i = 1; i <= 10; i++) {
                await counters.Increment(key, cancellationToken);
                await c.WhenInvalidated(cancellationToken).WaitAsync(timeout);
                c = await c.Update(cancellationToken);
                Out.WriteLine(c.ToString());
                c.Value.Should().Be(i);
            }
        }

    }
}
