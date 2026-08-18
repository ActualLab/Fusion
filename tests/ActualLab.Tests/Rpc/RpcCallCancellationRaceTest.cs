using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public class RpcCallCancellationRaceTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRemoteExecService, TestRemoteExecService>();
    }

    [Fact]
    public async Task ResultRacingCancellationMustNotDeadlockTest()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        // Completing a call takes its Lock and releases the cancellation registration, while the
        // registration's own callback (Cancel) takes that same Lock - so a result arriving exactly
        // when the token fires deadlocks both unless the release doesn't wait for the callback.
        var callDuration = TimeSpan.FromMilliseconds(20);
        var raceTask = Task.Run(async () => {
            for (var i = 0; i < 200; i++) {
                using var cts = new CancellationTokenSource(callDuration);
                try {
                    await client.DefaultDelay(callDuration, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    // Whether the result or the cancellation wins is irrelevant here
                }
            }
        });

        await raceTask.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
