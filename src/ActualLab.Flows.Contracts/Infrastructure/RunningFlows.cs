using ActualLab.CommandR;
using ActualLab.Fusion;
using ActualLab.Internal;

namespace ActualLab.Flows.Infrastructure;

public class RunningFlows : ProcessorBase, IHasServices
{
    private IFlows? _flows;

    public IServiceProvider Services { get; }
    public FlowRegistry Registry { get; }
    public IFlows Flows => _flows ??= Services.GetRequiredService<IFlows>();
    public ICommander Commander { get; }
    public StateFactory StateFactory { get; }
    public MomentClockSet Clocks { get; }
    public ILogger Log { get; }

    public ConcurrentDictionary<FlowId, RunningFlow> Items { get; set; } = new();

    public RunningFlows(IServiceProvider services)
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
        foreach (var (_, peer) in Items)
            disposeTasks.Add(peer.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    public RunningFlow Get(FlowId flowId)
    {
        if (Items.TryGetValue(flowId, out var result))
            return result;

        lock (Lock) {
            if (Items.TryGetValue(flowId, out result))
                return result;
            if (WhenDisposed != null)
                throw Errors.AlreadyDisposed(GetType());

            flowId.Require();
            result = Create(flowId).Start();
            Items[flowId] = result;
            return result;
        }
    }

    public async Task<long> Notify(FlowId flowId, object? @event, CancellationToken cancellationToken)
    {
        while (true) {
            var runner = Get(flowId);
            var notifyTask = runner.Notify(@event, cancellationToken);
            try {
                return await notifyTask.ConfigureAwait(false);
            }
            catch (ChannelClosedException) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!runner.StopToken.IsCancellationRequested)
                    throw;

                // runner is disposing - let's wait for its completion before requesting a new one
                await runner.WhenRunning!.ConfigureAwait(false);
            }
        }
    }

    // Protected methods

    protected virtual RunningFlow Create(FlowId flowId)
        => new(this, flowId);
}
