using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class InvalidationWhileComputingTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task InvalidatedOutputIsNeverConsistent()
    {
        var services = CreateServices();
        var service = services.GetRequiredService<GatedService>();
        for (var i = 0; i < 200; i++) {
            var (whenGateReached, gate) = service.CloseGate();
            var getTask = service.Get();
            await whenGateReached.WaitAsync(OneSecond);
            var computed = Computed.GetExisting(() => service.Get())!;
            computed.ConsistencyState.Should().Be(ConsistencyState.Computing);

            using (Invalidation.Begin())
                _ = service.Get();

            // A lock-free reader must never see the invalidated output as consistent
            var whenReaderStarted = TaskCompletionSourceExt.New<Unit>();
            // A dedicated thread: a spinning pool thread could starve the computation it waits for
            var readerTask = Task.Factory.StartNew(() => {
                whenReaderStarted.TrySetResult(default);
                var isConsistentSeen = false;
                var stopAt = CpuTimestamp.Now + TimeSpan.FromSeconds(2);
                while (CpuTimestamp.Now < stopAt) {
                    var state = computed.ConsistencyState;
                    if (state == ConsistencyState.Consistent)
                        isConsistentSeen = true;
                    else if (state == ConsistencyState.Invalidated)
                        return isConsistentSeen;
                }
                return true; // Never invalidated
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await whenReaderStarted.Task;
            gate.SetResult(default);
            await getTask.WaitAsync(OneSecond);
            (await readerTask.WaitAsync(OneSecond)).Should().BeFalse($"iteration {i}");
        }
    }

    [Fact]
    public async Task DelayedInvalidationKeepsOutputConsistent()
    {
        var services = CreateServices();
        var service = services.GetRequiredService<GatedService>();
        var (whenGateReached, gate) = service.CloseGate();
        var getTask = service.GetDelayed();
        await whenGateReached.WaitAsync(OneSecond);
        var computed = Computed.GetExisting(() => service.GetDelayed())!;

        using (Invalidation.Begin())
            _ = service.GetDelayed();
        gate.SetResult(default);
        await getTask.WaitAsync(OneSecond);

        // The invalidation arrived while computing, but InvalidationDelay still applies to it
        computed.IsConsistent().Should().BeTrue();
        await computed.WhenInvalidated().WaitAsync(OneSecond);
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<GatedService>();
    }
}

public class GatedService : IComputeService
{
    private volatile TaskCompletionSource<Unit>? _whenGateReached;
    private volatile Task? _whenGateOpened;

    [ComputeMethod]
    public virtual Task<int> Get()
        => PassGate();

    [ComputeMethod(InvalidationDelay = 0.2)]
    public virtual Task<int> GetDelayed()
        => PassGate();

    // The next computation waits for the returned gate to open
    public (Task WhenGateReached, TaskCompletionSource<Unit> Gate) CloseGate()
    {
        var gate = TaskCompletionSourceExt.New<Unit>();
        var whenGateReached = TaskCompletionSourceExt.New<Unit>();
        _whenGateReached = whenGateReached;
        _whenGateOpened = gate.Task;
        return (whenGateReached.Task, gate);
    }

    private async Task<int> PassGate()
    {
        var whenGateOpened = _whenGateOpened;
        _whenGateOpened = null;
        _whenGateReached?.TrySetResult(default);
        if (whenGateOpened is not null)
            await whenGateOpened.ConfigureAwait(false);
        return 1;
    }
}
