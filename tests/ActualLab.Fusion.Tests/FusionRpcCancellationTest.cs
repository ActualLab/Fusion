using ActualLab.Fusion.Tests.Services;
using ActualLab.Generators.Internal;
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

    [Theory(Timeout = 120_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCancelTest(bool useClient)
    {
        var services = CreateServices();
        var serverCounters = services.GetRequiredService<CounterService>();
        var counters = useClient ? services.GetRequiredService<ICounterService>() : serverCounters;
        var key = useClient ? "wait250" : "wait125";
        var cancellationDelay = TimeSpan.FromMilliseconds(50);
        var cancellationDelayThreshold = cancellationDelay + TimeSpan.FromMilliseconds(25);
        var timeout = Debugger.IsAttached
            ? TimeSpan.FromMinutes(10)
            : TimeSpan.FromSeconds(10);

        await counters.Get(key);
        await counters.Increment(key);

        await Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() => Test(i)))
            .Collect(100);

        async Task<Unit> Test(int index) {
            var mustCancel = RandomShared.NextDouble() < 0.5;
            await Task.Delay(TimeSpan.FromMilliseconds(RandomShared.NextDouble() * 30));
            var callStartedAt = CpuTimestamp.Now;

            if (!mustCancel) {
                var c = await counters.Get(key).WaitAsync(timeout);
                Out.WriteLine($"{index}: {c} in {callStartedAt.Elapsed.ToShortString()}");
                for (var j = 0; j < 100; j++)
                    await Task.Yield();
                _ = serverCounters.Increment(key);
                return default;
            }

            using var cts = new CancellationTokenSource(cancellationDelay);
            try {
                var c = await counters.Get(key, cts.Token).WaitAsync(timeout, CancellationToken.None);
                var elapsed = callStartedAt.Elapsed;
                if (cts.IsCancellationRequested && elapsed >= cancellationDelayThreshold)
                    Assert.Fail("Should not be here.");
                Out.WriteLine($"!!! {index}: {c} in {elapsed.ToShortString()}");
            }
            catch (OperationCanceledException) {
                var elapsed = callStartedAt.Elapsed;
                Out.WriteLine($"{index}: cancelled in {elapsed.ToShortString()}");
            }
            return default;
        }
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
