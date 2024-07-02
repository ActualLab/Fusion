using ActualLab.Async;
using ActualLab.CommandR;
using ActualLab.Text;
using ActualLab.Time;
using Pastel;

namespace Samples.MeshRpc.Services;

public class Tester(IServiceProvider services) : WorkerBase
{
    private readonly Dictionary<(Symbol HostId, Symbol ServiceName), int> _knownValues = new();
    private readonly Host _ownHost = services.GetRequiredService<Host>();
    private readonly ICounter _counter = services.GetRequiredService<ICounter>();
    private readonly IFusionCounter _fusionCounter = services.GetRequiredService<IFusionCounter>();
    private readonly ICommander _commander = services.Commander();

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, 10).Select(i => Test(i, cancellationToken)).ToArray();
        return Task.WhenAll(tasks);
    }

    private async Task Test(int index, CancellationToken cancellationToken)
    {
        var hostId = _ownHost.Id;
        var actionPeriod = TimeSpan.FromSeconds(0.1).ToRandom(0.5);
        var rnd = new Random();
        var useFusion = rnd.NextDouble() < 0.5;
        var serviceName = (Symbol)(useFusion ? nameof(IFusionCounter) : nameof(ICounter));
        while (true) {
            var shardRef = ShardRef.New(rnd.Next());
            var mustIncrement = rnd.NextDouble() < 0.25;
            if (mustIncrement) {
                var command = useFusion
                    ? (ICommand)new FusionCounter_Increment(shardRef)
                    : new Counter_Increment(shardRef);
                await _commander.Call(command, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"{hostId} T{index}: {serviceName}.Increment({shardRef})".Pastel(ConsoleColor.White));
            }
            else {
                var state = useFusion
                    ? await _fusionCounter.Get(shardRef, cancellationToken).ConfigureAwait(false)
                    : await _counter.Get(shardRef, cancellationToken).ConfigureAwait(false);
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
                var message = $"{hostId} T{index}: {serviceName}.Get({shardRef}) -> {state}";
                message = isFailed
                    ? message.PastelBg(ConsoleColor.DarkRed)
                    : message.Pastel(ConsoleColor.Gray);
                Console.WriteLine(message);
            }
            await Task.Delay(actionPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }
}
