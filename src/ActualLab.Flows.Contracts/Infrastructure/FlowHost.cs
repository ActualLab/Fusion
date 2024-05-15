using ActualLab.CommandR;
using ActualLab.Fusion;
using ActualLab.Internal;

namespace ActualLab.Flows.Infrastructure;

public class FlowHost : ProcessorBase, IHasServices
{
    private IFlows? _flows;

    public IServiceProvider Services { get; }
    public FlowRegistry Registry { get; }
    public IFlows Flows => _flows ??= Services.GetRequiredService<IFlows>();
    public ICommander Commander { get; }
    public StateFactory StateFactory { get; }
    public MomentClockSet Clocks { get; }
    public ILogger Log { get; }

    public ConcurrentDictionary<FlowId, FlowWorker> Workers { get; set; } = new();

    public FlowWorker this[FlowId flowId] {
        get {
            if (Workers.TryGetValue(flowId, out var result))
                return result;

            lock (Lock) {
                if (Workers.TryGetValue(flowId, out result))
                    return result;
                if (WhenDisposed != null)
                    throw Errors.AlreadyDisposed(GetType());

                flowId.Require();
                result = Create(flowId).Start();
                Workers[flowId] = result;
                return result;
            }
        }
    }

    public FlowHost(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());

        Registry = services.GetRequiredService<FlowRegistry>();
        Commander = services.Commander();
        StateFactory = services.StateFactory();
        Clocks = services.Clocks();
    }

    protected override Task DisposeAsyncCore()
    {
        var disposeTasks = new List<Task>();
        foreach (var (_, peer) in Workers)
            disposeTasks.Add(peer.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    public async Task<long> Notify(FlowId flowId, object? @event, CancellationToken cancellationToken)
    {
        while (true) {
            var worker = this[flowId];
            var notifyTask = worker.Notify(@event, cancellationToken);
            try {
                return await notifyTask.ConfigureAwait(false);
            }
            catch (ChannelClosedException) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!worker.StopToken.IsCancellationRequested)
                    throw;

                // runner is disposing - let's wait for its completion before requesting a new one
                await worker.WhenRunning!.ConfigureAwait(false);
            }
        }
    }

    // Protected methods

    protected virtual FlowWorker Create(FlowId flowId)
        => new(this, flowId);
}
