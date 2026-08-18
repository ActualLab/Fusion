using ActualLab.Testing.Logging;

namespace ActualLab.Fusion.Tests;

public class ComputedStateGracefulDisposeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task DisposeDuringUpdateMustNotRetryTest()
    {
        // GracefulDisposeToken stays live for GracefulDisposeDelay after DisposeToken fires, and
        // UpdateCycle updates on it - so a dispose landing mid-update cancels the computation with
        // one token while the retry classifier is asked about the other. Misreading that as an
        // internal cancellation retries a permanently cancelled computation until MaxDuration.
        var logs = new CapturingLoggerProvider();
        var services = CreateServices(services => services.AddLogging(x => x.AddProvider(logs)));

        var whenComputing = TaskCompletionSourceExt.New<Unit>();
        var callCount = 0;
        var state = services.StateFactory().NewComputed(
            new ComputedState<int>.Options() {
                InitialOutput = -1,
                UpdateDelayer = FixedDelayer.NextTick,
            },
            async (s, cancellationToken) => {
                if (Interlocked.Increment(ref callCount) == 1)
                    return 1; // The initial update must succeed, so the cycle reaches its update loop

                whenComputing.TrySetResult(default);
                // Cancelled by DisposeToken, while the cycle awaits on GracefulDisposeToken
                await Task.Delay(Timeout.InfiniteTimeSpan, s.DisposeToken).ConfigureAwait(false);
                return 2;
            });

        (await state.Use()).Should().Be(1);

        state.Computed.Invalidate(); // Pushes the cycle into UpdateUntyped(GracefulDisposeToken)
        await whenComputing.Task.WaitAsync(TimeSpan.FromSeconds(5));

        state.Dispose(); // Lands mid-update: DisposeToken fires, GracefulDisposeToken doesn't
        await Delay(1); // Well past the first retry delay (~50ms), well within MaxDuration (2s)

        var retries = logs.Content.Split("was cancelled internally").Length - 1;
        Out.WriteLine($"Compute calls: {callCount}, retry warnings: {retries}");
        retries.Should().Be(0);
    }
}
