namespace ActualLab.Flows.Infrastructure;

public class LiveFlow : WorkerBase
{
    protected Channel<EventEntry> Events { get; init; } = Channel.CreateUnbounded<EventEntry>(new() {
        SingleReader = true,
        SingleWriter = false,
    });

    public LiveFlowSet Owner { get; }
    public FlowId FlowId { get; }
    public Flow? Flow { get; protected set; }
    public ILogger Log { get; }

    public LiveFlow(LiveFlowSet owner, FlowId flowId)
    {
        Owner = owner;
        FlowId = flowId;
        var flowType = owner.Registry.Types[flowId.Name];
        Log = owner.Services.LogFor(flowType);
    }

    public async ValueTask<Task<long>> Notify(object @event, CancellationToken cancellationToken)
    {
        var entry = new EventEntry(@event, cancellationToken, TaskCompletionSourceExt.New<long>());
        await Events.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry.ResultSource.Task;
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var flow = await Owner.Flows.GetOrStart(FlowId, cancellationToken).ConfigureAwait(false);
        Flow = flow.Clone();
        Flow.Initialize(flow.Id, flow.Version, this);
        var options = Flow.GetOptions();
        var reader = Events.Reader;
        while (true) {
            try {
                var waitToReadTask = reader.WaitToReadAsync(cancellationToken);
                var canRead = waitToReadTask.IsCompleted
                    ? await waitToReadTask.ConfigureAwait(false)
                    : await waitToReadTask.AsTask()
                        .WaitAsync(options.KeepAliveFor, cancellationToken)
                        .ConfigureAwait(false);
                if (!canRead)
                    return;
            }
            catch (Exception e) when (e is TimeoutException || e.IsCancellationOf(cancellationToken)) {
                return;
            }
            if (!reader.TryRead(out var entry))
                continue;

            if (entry.CancellationToken.IsCancellationRequested) {
                entry.ResultSource.TrySetCanceled(entry.CancellationToken);
                continue;
            }

            flow = Flow.Clone();
            try {
                await Flow.MoveNext(entry.Event, cancellationToken).ConfigureAwait(false);
                entry.ResultSource.TrySetResult(Flow.Version);
            }
            catch (Exception e) {
                Flow = flow; // Restore pre-error state
                if (e.IsCancellationOf(cancellationToken)) {
                    entry.ResultSource.TrySetCanceled(cancellationToken);
                    throw;
                }

                entry.ResultSource.TrySetException(e);
                Log.LogError("'{Id}' @ {NextStep} failed", flow.Id, flow.NextStep);
            }
        }
    }

    protected override Task OnStop()
    {
        Events.Writer.TryComplete();
        _ = DisposeAsync();
        Owner.Items.TryRemove(FlowId, this);
        return Task.CompletedTask;
    }

    // Nested types

#pragma warning disable CA1068
    public readonly record struct EventEntry(
        object Event,
        CancellationToken CancellationToken,
        TaskCompletionSource<long> ResultSource);
#pragma warning restore CA1068
}
