using ActualLab.Fusion.Tests.Services;
using ActualLab.Generators;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcCancellationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<ICounterService, CounterService>();
    }

    [Fact]
    public async Task CancelTest()
    {
        var services = CreateServices();
        var client = services.RpcHub().GetClient<ICounterService>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()
            => await Computed.Capture(() => client.Get("cancel")));

        using var cts = new CancellationTokenSource(100);
        // ReSharper disable once MethodSupportsCancellation
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()
            => await Computed.Capture(() => client.Get("wait", cts.Token)));
    }

    [Theory(Timeout = 120_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCancelTest(bool useClient)
    {
        var services = CreateServices();
        var server = services.RpcHub().GetServer<ICounterService>();
        var counters = useClient
            ? services.RpcHub().GetClient<ICounterService>()
            : server;
        var waitSuffix = "-wait250";
        var cancellationDelay = TimeSpan.FromMilliseconds(50);
        var cancellationDelayThreshold = cancellationDelay + TimeSpan.FromMilliseconds(25);
        var timeout = Debugger.IsAttached
            ? TimeSpan.FromMinutes(10)
            : TimeSpan.FromSeconds(30);

        await Enumerable.Range(0, 1_000)
            .Select(i => Task.Run(() => Test(i)))
            .Collect();

        async Task<Unit> Test(int index) {
            var key = $"{index % 10}-{waitSuffix}";
            var mustCancel = RandomShared.NextDouble() < 0.5;
            await Task.Delay(TimeSpan.FromMilliseconds(RandomShared.NextDouble() * 30));
            var callStartedAt = CpuTimestamp.Now;

            if (!mustCancel) {
                try {
                    var c = await counters.Get(key).WaitAsync(timeout, CancellationToken.None);
                    Out.WriteLine($"{index}: {c} in {callStartedAt.Elapsed.ToShortString()}");
                    for (var j = 0; j < 100; j++)
                        await Task.Yield();
                    _ = server.Increment(key);
                    return default;
                }
                catch (TimeoutException) {
                    Out.WriteLine($"!!! {index}: timed-out (1) in {callStartedAt.Elapsed.ToShortString()}");
                    throw;
                }
            }

            using var cts = new CancellationTokenSource(cancellationDelay);
            try {
                var c = await counters.Get(key, cts.Token).WaitAsync(timeout, CancellationToken.None);
                var elapsed = callStartedAt.Elapsed;
                if (cts.IsCancellationRequested && elapsed >= cancellationDelayThreshold)
                    Assert.Fail("Should not be here.");
                Out.WriteLine($"{index}: {c} in {elapsed.ToShortString()}");
            }
            catch (TimeoutException) {
                Out.WriteLine($"!!! {index}: timed-out (2) in {callStartedAt.Elapsed.ToShortString()}");
                throw;
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
        var client = services.RpcHub().GetClient<ICounterService>();
        var key = "cancel50";
        var timeout = TimeSpan.FromSeconds(1);

        // ReSharper disable once MethodSupportsCancellation
        var c = await Computed.Capture(() => client.Get(key));
        c.Value.Should().Be(0);

        // Test w/o CancellationToken
        await Test(CancellationToken.None);
        await client.Set(key, 0); // Post-test reset
        await Task.Delay(500);

        // Test with CancellationToken
        using var cts = new CancellationTokenSource(60_000);
        await Test(cts.Token);

        async Task Test(CancellationToken cancellationToken) {
            c = await Computed.Capture(() => client.Get(key, cancellationToken))
                .AsTask().WaitAsync(timeout);
            for (var i = 1; i <= 10; i++) {
                await client.Increment(key, cancellationToken);
                await c.WhenInvalidated(cancellationToken).WaitAsync(timeout);
                c = await c.Update(cancellationToken);
                Out.WriteLine(c.ToString());
                c.Value.Should().Be(i);
            }
        }
    }
}
