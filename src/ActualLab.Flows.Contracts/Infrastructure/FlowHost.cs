using ActualLab.CommandR;
using ActualLab.Internal;

namespace ActualLab.Flows.Infrastructure;

public class FlowHost : ProcessorBase, IHasServices
{
    private IFlows? _flows;

    public IServiceProvider Services { get; }
    public FlowRegistry Registry { get; }
    public IFlows Flows => _flows ??= Services.GetRequiredService<IFlows>();
    public ICommander Commander { get; }
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
        Clocks = services.Clocks();
    }

    protected override Task DisposeAsyncCore()
    {
        var disposeTasks = new List<Task>();
        foreach (var (_, worker) in Workers)
            disposeTasks.Add(worker.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    public async Task<long> HandleEvent(FlowId flowId, object? evt, CancellationToken cancellationToken)
    {
        while (true) {
            var worker = this[flowId];
            var whenHandled = worker.HandleEvent(evt, cancellationToken);
            try {
                return await whenHandled.ConfigureAwait(false);
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
