using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Client.Internal;

#pragma warning disable RCS1210, MA0022

/// <summary>
/// An inbound RPC call that tracks the associated <see cref="Computed"/> and sends
/// invalidation notifications back to the caller.
/// </summary>
public abstract class RpcInboundComputeCall : RpcInboundCall
{
    public override string DebugTypeName => "<=";
    public override int CompletedStage
        => ResultTask is { IsCompleted: true } ? (UntypedComputed is { } c && c.IsInvalidated() ? 2 : 1) : 0;
    public override string CompletedStageName
        => CompletedStage switch { 0 => "", 1 => "ResultReady", _ => "Invalidated" };
    public abstract Computed? UntypedComputed { get; }

    protected RpcInboundComputeCall(RpcInboundContext context)
        : base(context)
    {
        if (NoWait)
            throw Errors.InternalError($"{GetType().GetName()} is incompatible with NoWait option.");
    }

    // Protected & private methods

    public override Task? TryReprocess(int completedStage, CancellationToken cancellationToken)
    {
        lock (Lock) {
            var existingCall = Context.Peer.InboundCalls.Get(Id);
            if (existingCall != this || ResultTask is null)
                return null;

            return WhenProcessed = completedStage switch {
                >= 2 => Task.CompletedTask,
                1 => ProcessStage2(cancellationToken),
                _ => ProcessStage1Plus(cancellationToken)
            };
        }
    }

    protected override async Task ProcessStage1Plus(CancellationToken cancellationToken)
    {
        await ResultTask!.SilentAwait(false);
        lock (Lock) {
            if (Trace is { } trace) {
                trace.Complete(this);
                Trace = null;
            }
            if (CallCancelToken.IsCancellationRequested) {
                // The call is cancelled by the remote party
                UnregisterFromLock();
                return;
            }
        }
        SendResult();
        await ProcessStage2(cancellationToken).ConfigureAwait(false);
    }

    protected async Task ProcessStage2(CancellationToken cancellationToken)
    {
        var mustSendInvalidation = true;
        try {
            if (UntypedComputed is { } computed) {
                using var commonCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CallCancelToken);
                await computed.WhenInvalidated(commonCts.Token).ConfigureAwait(false);
            }
            else
                await TickSource.Default
                    .WhenNextTick()
                    .ConfigureAwait(false); // A bit of extra delay in case there is no computed
        }
        catch (OperationCanceledException) when (CallCancelToken.IsCancellationRequested) {
            // The call is cancelled by the remote party
            mustSendInvalidation = false;
        }
        Unregister();
        if (mustSendInvalidation) {
            var computeSystemCallSender = Hub.Services.GetRequiredService<RpcComputeSystemCallSender>();
            computeSystemCallSender.Invalidate(Context.Peer, Id, ResultHeaders);
        }
    }
}

/// <summary>
/// A strongly-typed <see cref="RpcInboundComputeCall"/> for a specific result type.
/// </summary>
public sealed class RpcInboundComputeCall<TResult>(RpcInboundContext context)
    : RpcInboundComputeCall(context)
{
    public Computed<TResult>? Computed;
    public override Computed? UntypedComputed => Computed;

    protected override void SendResult()
        => DefaultSendResult((Task<TResult>?)ResultTask);
}
