using ActualLab.Async;
using ActualLab.Time;
using Pastel;

namespace Samples.MeshRpc.Services;

public class Tester(IServiceProvider services) : WorkerBase
{
    private readonly Host _ownHost = services.GetRequiredService<Host>();
    private readonly ICounter _counter = services.GetRequiredService<ICounter>();
    private readonly IFusionCounter _fusionCounter = services.GetRequiredService<IFusionCounter>();

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, 10).Select(i => Test(i, cancellationToken)).ToArray();
        return Task.WhenAll(tasks);
    }

    private async Task Test(int index, CancellationToken cancellationToken)
    {
        var hostId = _ownHost.Id;
        var actionPeriod = TimeSpan.FromSeconds(0.1).ToRandom(0.5);
        while (true) {
            var shardRef = ShardRef.New(Random.Shared.Next());
            var state = await _counter.Get(shardRef, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"{hostId} T{index}: Get({shardRef}) -> {state}".Pastel(ConsoleColor.Gray));

            await Task.Delay(actionPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }
}
