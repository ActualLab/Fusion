using ActualLab;
using ActualLab.Async;
using ActualLab.CommandR;
using ActualLab.Rpc;
using ActualLab.Text;
using Pastel;
using static Samples.MeshRpc.TestSettings;

namespace Samples.MeshRpc.Services;

public class Tester(IServiceProvider services) : WorkerBase
{
    private readonly Dictionary<(Symbol HostId, Symbol ServiceName), int> _knownValues = new();

    private Host OwnHost { get; } = services.GetRequiredService<Host>();
    private ICounter Counter { get; } = services.GetRequiredService<ICounter>();
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
            var useFusion = rnd.NextDouble() <= FusionServiceUseProbability;
            var serviceName = (Symbol)(useFusion ? nameof(IFusionCounter) : nameof(ICounter));
            var mustIncrement = rnd.NextDouble() <= IncrementProbability;
            var shardRef = ShardRef.New(rnd.Next());

            var prefix = $"{OwnHost} T{workerId}/{callId}: {serviceName}";
            if (mustIncrement) {
                prefix += $".Increment({shardRef})";
                Console.WriteLine($"{prefix}...".Pastel(ConsoleColor.Gray));
                var command = useFusion
                    ? (ICommand)new FusionCounter_Increment(shardRef)
                    : new Counter_Increment(shardRef);
                await Commander.Call(command, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"{prefix} -> ok");
            }
            else {
                prefix += $".Get({shardRef})";
                Console.WriteLine($"{prefix}...".Pastel(ConsoleColor.Gray));
                var state = useFusion
                    ? await FusionCounter.Get(shardRef, cancellationToken).ConfigureAwait(false)
                    : await Counter.Get(shardRef, cancellationToken).ConfigureAwait(false);
                var isFailed = false;
                lock (_knownValues) {
                    var key = (state.HostId, serviceName);
                    if (!_knownValues.TryGetValue(key, out var lastValue))
                        _knownValues[key] = state.Value;
                    else {
                        _knownValues[key] = state.Value;
                        if (state.Value < lastValue)
                            isFailed = true;
                    }
                }
                var message = $"{prefix} -> {state}";
                if (isFailed)
                    message = message.PastelBg(ConsoleColor.DarkRed);
                Console.WriteLine(message);
            }
            await Task.Delay(CallPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }
}
