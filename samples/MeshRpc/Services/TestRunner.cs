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

        using var stopTokenSource = cancellationToken.CreateDelayedTokenSource(TestStopDelay);
        cancellationToken = stopTokenSource.Token;
        var testTasks = Enumerable.Range(0, ProcessesPerHost)
            .Select(async workerId => {
                try {
                    await Test(workerId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    if (Services.IsDisposedOrDisposing())
                        return;

                    await Console.Error.WriteLineAsync($"{OwnHost} T{workerId} failed: {e.Message}".PastelBg(ConsoleColor.DarkRed));
                }
            })
            .ToArray();
        return Task.WhenAll(testTasks);
    }

    private async Task Test(int workerId, CancellationToken cancellationToken)
    {
        var rnd = new Random();
        for (var callId = 0;; callId++) {
            var useFusion = UseFusionSampler.Next();
            var serviceName = useFusion ? nameof(IFusionCounter) : nameof(ISimpleCounter);
            var mustIncrement = IncrementSampler.Next();
            var key = rnd.Next(CounterCount);

            var prefix = $"{OwnHost} W{workerId}: call #{callId} {serviceName}";
            if (mustIncrement) {
                prefix += $".Increment({key})";
                Console.WriteLine($"{prefix}...".Pastel(ConsoleColor.Gray));
                var incrementTask = useFusion
                    ? Commander.Call(new FusionCounter_Increment(key), cancellationToken)
                    : Commander.Call(new SimpleCounter_Increment(key), cancellationToken);
                var result = await incrementTask.ConfigureAwait(false);
                Console.WriteLine($"{prefix} -> {result}");
            }
            else {
                prefix += $".Get({key})";
                Console.WriteLine($"{prefix}...".Pastel(ConsoleColor.Gray));

                var useFusionCopy = useFusion;
                var shardRefCopy = key;
                var computed = await Computed.TryCapture(
                    () => useFusionCopy
                        ? FusionCounter.Get(shardRefCopy, cancellationToken)
                        : SimpleCounter.Get(shardRefCopy, cancellationToken)
                    , cancellationToken).ConfigureAwait(false);

                var counter = useFusion
                    ? await FusionCounter.Get(key, cancellationToken).ConfigureAwait(false)
                    : await SimpleCounter.Get(key, cancellationToken).ConfigureAwait(false);
                var message = $"{prefix} -> {counter}";

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
                if (fixupActions.Count != 0)
                    Console.WriteLine($"{message}: {fixupActions.ToDelimitedString()}".PastelBg(ConsoleColor.DarkYellow));

                if (!isCorrect) {
                    var recency = counter.Counter.UpdatedAt.Elapsed;
                    message = $"{message} - {counter.Counter.Value} < {trueCounter.Value}, read {recency.ToShortString()} ago"
                        .PastelBg(ConsoleColor.DarkRed);
                }
                Console.WriteLine(message);
            }
            await Task.Delay(CallPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public static bool IsCorrect(CounterWithOrigin counterWithOrigin, Counter trueCounter)
    {
        var counter = counterWithOrigin.Counter;
        return counter.Value >= trueCounter.Value;
    }
}
