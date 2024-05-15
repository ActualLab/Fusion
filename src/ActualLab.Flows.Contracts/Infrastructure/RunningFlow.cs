using ActualLab.Fusion.Internal;

namespace ActualLab.Flows.Infrastructure;

public class RunningFlow : WorkerBase, IGenericTimeoutHandler
{
    protected static readonly ChannelClosedException ChannelClosedExceptionInstance = new();

    protected Channel<QueueEntry> Queue { get; set; }
    protected ChannelWriter<QueueEntry> Writer { get; init; }

    public RunningFlows Owner { get; }
    public FlowId FlowId { get; }
    public ILogger Log { get; }

    public RunningFlow(RunningFlows owner, FlowId flowId)
    {
        Owner = owner;
        FlowId = flowId;
        var flowType = owner.Registry.Types[flowId.Name];
        Log = owner.Services.LogFor(flowType);

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

    public Task<long> Notify(object? @event, CancellationToken cancellationToken)
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
        var flow = await Owner.Flows.GetOrStart(FlowId, cancellationToken).ConfigureAwait(false);
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
                    await flow.MoveNext(entry.Event, gracefulStopToken).ConfigureAwait(false);
                    entry.ResultSource.TrySetResult(flow.Version);
                }
                catch (Exception e) {
                    flow = backup;
                    if (e.IsCancellationOf(gracefulStopToken)) {
                        entry.ResultSource.TrySetCanceled(gracefulStopToken);
                        throw;
                    }

                    entry.ResultSource.TrySetException(e);
                    Log.LogError("'{Id}' @ {NextStep} failed", flow.Id, flow.NextStep);
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
            Owner.Items.TryRemove(FlowId, this);
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
