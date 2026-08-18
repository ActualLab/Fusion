using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class PreciseInvalidationDeadlockTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task PreciseInvalidationMustNotWaitForItsTimerCallbackTest()
    {
        var services = CreateServicesWithComputeService<CounterService>();
        var counters = services.GetRequiredService<CounterService>();
        var computed = await Computed.Capture(() => counters.Get("deadlock"));

        // Arms the precise-timer path: a CancellationTokenSource plus a registration whose
        // callback invalidates the computed, and an Invalidated handler that releases that
        // registration. Computed.Lock is the computed itself, so the lock below is the very
        // one the callback needs - which is what the Invalidated event's add accessor holds
        // when it invokes a handler inline on an already-invalidated computed.
        computed.Invalidate(TimeSpan.FromMilliseconds(100), usePreciseTimer: true);

        var raceTask = Task.Run(() => {
            lock (computed) {
                Thread.Sleep(500); // Lets the timer callback reach Invalidate() and block here
                computed.Invalidate(immediately: true); // Runs the Invalidated handler under the lock
            }
        });

        var isCompleted = await raceTask.WaitAsync(TimeSpan.FromSeconds(10)).ResultAwait();
        isCompleted.Error.Should().BeNull();
    }

    [Fact]
    public async Task WhenInvalidatedMustNotWaitForItsCancellationCallbackTest()
    {
        var services = CreateServicesWithComputeService<CounterService>();
        var counters = services.GetRequiredService<CounterService>();
        var computed = await Computed.Capture(() => counters.Get("deadlock2"));

        using var cts = new CancellationTokenSource();
        _ = computed.WhenInvalidated(cts.Token); // Arms the closure and its cancellation registration

        var raceTask = Task.Run(() => {
            lock (computed) {
                // Cancelling from another thread parks the closure's OnUnregister on this lock,
                // since it unsubscribes via the Invalidated event's remove accessor
                _ = Task.Run(() => cts.Cancel());
                Thread.Sleep(500);
                computed.Invalidate(immediately: true); // Runs OnInvalidated under the lock
            }
        });

        var isCompleted = await raceTask.WaitAsync(TimeSpan.FromSeconds(10)).ResultAwait();
        isCompleted.Error.Should().BeNull();
    }
}
