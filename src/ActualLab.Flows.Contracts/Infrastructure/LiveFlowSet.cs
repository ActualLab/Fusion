using ActualLab.CommandR;
using ActualLab.Internal;

namespace ActualLab.Flows.Infrastructure;

public class LiveFlowSet : ProcessorBase, IHasServices
{
    private IFlows? _flows;

    public IServiceProvider Services { get; }
    public FlowRegistry Registry { get; }
    public IFlows Flows => _flows ??= Services.GetRequiredService<IFlows>();
    public MomentClockSet Clocks { get; }
    public ICommander Commander { get; }
    public ILogger Log { get; }

    public ConcurrentDictionary<FlowId, LiveFlow> Items { get; set; } = new();

    public LiveFlowSet(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());
        Clocks = services.Clocks();
        Commander = services.Commander();
        Registry = services.GetRequiredService<FlowRegistry>();
    }

    protected override Task DisposeAsyncCore()
    {
        var disposeTasks = new List<Task>();
        foreach (var (_, peer) in Items)
            disposeTasks.Add(peer.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    public LiveFlow Get(FlowId flowId)
    {
        if (Items.TryGetValue(flowId, out var result))
            return result;

        lock (Lock) {
            if (Items.TryGetValue(flowId, out result))
                return result;
            if (WhenDisposed != null)
                throw Errors.AlreadyDisposed(GetType());

            result = Create(flowId);
            Items[flowId] = result;
            result.Start();
            return result;
        }
    }

    // Protected methods

    protected virtual LiveFlow Create(FlowId flowId)
        => new(this, flowId);
}
