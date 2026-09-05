using ActualLab.Rpc;
using Pastel;
using static Samples.MeshRpc.TestSettings;

namespace Samples.MeshRpc.Services;

public class TestRunner(IServiceProvider services) : WorkerBase
{
    private IServiceProvider Services { get; } = services;
    private Host OwnHost { get; } = services.GetRequiredService<Host>();
    private ISimpleCounter SimpleCounter { get; } = services.GetRequiredService<ISimpleCounter>();
    private IFusionCounter FusionCounter { get; } = services.GetRequiredService<IFusionCounter>();
    private ICommander Commander { get; } = services.Commander();

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var isClient = OwnHost.ServiceMode == RpcServiceMode.Client;
        var mustRun = isClient ? MustRunOnClientHost : MustRunOnServerHost;
        if (!mustRun)
            return Task.CompletedTask;

        var testTasks = Enumerable.Range(0, ProcessesPerHost)
            .Select(async workerId => {
                try {
                    await Test(workerId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    if (Services.IsDisposedOrDisposing())
                        return;

                    await Console.Error.WriteLineAsync(
                        $"{OwnHost} T{workerId} failed: {e.GetType().Name}({e.Message}), {e.StackTrace}".PastelBg(ConsoleColor.DarkRed));
                }
            })
            .ToArray();
        return Task.WhenAll(testTasks);
    }

    public static bool IsCorrect(CounterWithOrigin counterWithOrigin, Counter trueCounter)
    {
        var counter = counterWithOrigin.Counter;
        return counter.Value >= trueCounter.Value;
    }

    // Private methods

    private async Task Test(int workerId, CancellationToken cancellationToken)
    {
        var rnd = new Random();
        for (var callId = 0;; callId++) {
            var useFusion = UseFusionSampler.Next();
            var mustIncrement = IncrementSampler.Next();
            var key = rnd.Next(CounterCount);
            var callKind = (useFusion ? nameof(IFusionCounter) : nameof(ISimpleCounter))
                + (mustIncrement ? ".Increment" : ".Get");

            TestCallOutcome outcome;
            try {
                outcome = mustIncrement
                    ? await Increment(useFusion, key, cancellationToken).ConfigureAwait(false)
                    : await Get(workerId, callId, useFusion, key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                // Counted and logged, but not fatal: one failed call shouldn't end the worker
                outcome = TestCallOutcome.Error;
                Console.WriteLine(
                    $"{Prefix(workerId, callId, callKind, key)}: {e.GetType().Name}({e.Message})"
                        .PastelBg(ConsoleColor.DarkRed));
            }
            TestStats.Register(callKind, outcome);

            await Task.Delay(CallPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task<TestCallOutcome> Increment(bool useFusion, int key, CancellationToken cancellationToken)
    {
        var command = useFusion
            ? (ICommand<CounterWithOrigin>)new FusionCounter_Increment(key)
            : new SimpleCounter_Increment(key);
        await Commander.Call(command, cancellationToken).ConfigureAwait(false);
        return TestCallOutcome.Ok;
    }

    private async Task<TestCallOutcome> Get(
        int workerId, int callId, bool useFusion, int key, CancellationToken cancellationToken)
    {
        var computed = await Computed.TryCapture(
            () => useFusion
                ? FusionCounter.Get(key, cancellationToken)
                : SimpleCounter.Get(key, cancellationToken)
            , cancellationToken).ConfigureAwait(false);

        var counter = useFusion
            ? await FusionCounter.Get(key, cancellationToken).ConfigureAwait(false)
            : await SimpleCounter.Get(key, cancellationToken).ConfigureAwait(false);

        var trueCounter = CounterStorage.Get(key);
        var fixupActions = new List<string>();
        var isCorrect = IsCorrect(counter, trueCounter);
        for (var tryIndex = 0; !isCorrect && tryIndex < MaxRetryCount; tryIndex++) {
            if (tryIndex > 0) {
                fixupActions.Add("wait 200ms");
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            if (computed is not null) {
                // The logic below does ~ what computed.Synchronize() does, but step-by-step w/ logging
                var whenSynchronized = computed.WhenSynchronized(cancellationToken);
                if (!whenSynchronized.IsCompleted) {
                    fixupActions.Add("synchronize");
                    await whenSynchronized.ConfigureAwait(false); // Completes when a value from RemoteComputedCache is in sync or unused
                }
                if (!computed.IsConsistent()) {
                    fixupActions.Add("update inconsistent");
                    computed = await computed.Update(cancellationToken).ConfigureAwait(false);
                }
                counter = computed.Value;
            }
            else {
                fixupActions.Add("retry .Get call");
                counter = await SimpleCounter.Get(key, cancellationToken).ConfigureAwait(false);
            }
            isCorrect = IsCorrect(counter, trueCounter);
        }

        var callKind = (useFusion ? nameof(IFusionCounter) : nameof(ISimpleCounter)) + ".Get";
        var message = $"{Prefix(workerId, callId, callKind, key)} -> {counter}";
        if (!isCorrect) {
            var recency = counter.Counter.UpdatedAt.Elapsed;
            Console.WriteLine(
                ($"{message}: {counter.Counter.Value} < {trueCounter.Value}, read {recency.ToShortString()} ago"
                    + $", {fixupActions.ToDelimitedString()}").PastelBg(ConsoleColor.DarkRed));
            return TestCallOutcome.Failed;
        }

        if (fixupActions.Count == 0)
            return TestCallOutcome.Ok; // The normal case prints nothing - see TestStats

        Console.WriteLine($"{message}: {fixupActions.ToDelimitedString()}".PastelBg(ConsoleColor.DarkYellow));
        return TestCallOutcome.Warn;
    }

    private string Prefix(int workerId, int callId, string callKind, int key)
        => $"{OwnHost} W{workerId}: call #{callId} {callKind}({key})";
}
