using ActualLab.Fusion.Internal;

namespace ActualLab.Flows.Infrastructure;

public class FlowWorker : WorkerBase, IGenericTimeoutHandler
{
    protected static readonly ChannelClosedException ChannelClosedExceptionInstance = new();

    protected Channel<QueueEntry> Queue { get; set; }
    protected ChannelWriter<QueueEntry> Writer { get; init; }

    public FlowHost Host { get; }
    public FlowId FlowId { get; }
    public ILogger Log { get; }

    public FlowWorker(FlowHost host, FlowId flowId)
    {
        Host = host;
        FlowId = flowId;
        var flowType = host.Registry.Types[flowId.Name];
        Log = host.Services.LogFor(flowType);

        Queue = Channel.CreateUnbounded<QueueEntry>(new() {
            SingleReader = true,
            SingleWriter = true,
        });
        Writer = Queue.Writer;
    }

    protected override Task DisposeAsyncCore()
    {
        // DisposeAsyncCore always runs inside lock (Lock)
        Writer.TryComplete();
        return base.DisposeAsyncCore();
    }

    public override string ToString()
        => $"{GetType().Name}('{FlowId}')";

    public Task<long> HandleEvent(object? @event, CancellationToken cancellationToken)
    {
        var entry = new QueueEntry(@event, cancellationToken);
        bool couldWrite;
        lock (Lock)
            couldWrite = Writer.TryWrite(entry);
        if (!couldWrite)
            entry.ResultSource.TrySetException(ChannelClosedExceptionInstance);
        return entry.ResultSource.Task;
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var flow = await Host.Flows.GetOrStart(FlowId, cancellationToken).ConfigureAwait(false);
        flow = flow.Clone();
        flow.Initialize(flow.Id, flow.Version, this);
        var options = flow.GetOptions();

        var clock = Timeouts.Generic.Clock;
        var reader = Queue.Reader;

        var gracefulStopCts = cancellationToken.CreateDelayedTokenSource(options.GracefulDisposeDelay);
        var gracefulStopToken = gracefulStopCts.Token;
        try {
            while (true) {
                // We don't pass cancellationToken to WaitToReadAsync, coz
                // the channel is reliably getting closed on dispose -
                // see DisposeAsyncCore and IGenericTimeoutHandler.OnTimeout.
                var canReadTask = reader.WaitToReadAsync(CancellationToken.None);
                if (canReadTask.IsCompleted) {
                    if (!await canReadTask.ConfigureAwait(false))
                        return;
                }
                else {
                    Timeouts.Generic.AddOrUpdateToLater(this, clock.Now + options.KeepAliveFor);
                    if (!await canReadTask.ConfigureAwait(false))
                        return;
                    Timeouts.Generic.Remove(this);
                }
                if (!reader.TryRead(out var entry))
                    continue;

                if (entry.CancellationToken.IsCancellationRequested) {
                    entry.ResultSource.TrySetCanceled(entry.CancellationToken);
                    continue;
                }

                var backup = flow.Clone();
                try {
                    var @event = entry.Event;
                    while (true) {
                        var transition = await flow.HandleEvent(@event, gracefulStopToken).ConfigureAwait(false);
                        if (!transition.IsImmediate)
                            break;
                        @event = null;
                    }
                    entry.ResultSource.TrySetResult(flow.Version);
                }
                catch (Exception e) {
                    flow = backup;
                    if (e.IsCancellationOf(gracefulStopToken)) {
                        entry.ResultSource.TrySetCanceled(gracefulStopToken);
                        throw;
                    }

                    entry.ResultSource.TrySetException(e);
                    Log.LogError("'{Id}' @ {NextStep} failed", flow.Id, flow.Step);
                }
            }
        }
        finally {
            gracefulStopCts.CancelAndDisposeSilently();
            try {
                // Cancel remaining queued entries
                await foreach (var entry in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                    entry.ResultSource.TrySetException(ChannelClosedExceptionInstance);
            }
            catch {
                // Intended
            }
            Host.Workers.TryRemove(FlowId, this);
        }
    }

    void IGenericTimeoutHandler.OnTimeout()
    {
        lock (Lock) {
            if (!Queue.Reader.TryPeek(out _)) // Don't shut down when there are queued items
                _ = DisposeAsync();
        }
    }

    // Nested types

    public readonly record struct QueueEntry(
        object? Event,
        CancellationToken CancellationToken
    ) {
        public TaskCompletionSource<long> ResultSource { get; init; } = new();
    }
}
