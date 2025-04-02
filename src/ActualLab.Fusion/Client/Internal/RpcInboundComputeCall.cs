using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Client.Internal;

#pragma warning disable RCS1210, MA0022

public abstract class RpcInboundComputeCall : RpcInboundCall
{
    public override string DebugTypeName => "<=";
    public override int CompletedStage
        => ResultTask is { IsCompleted: true } ? (UntypedComputed is { } c && c.IsInvalidated() ? 2 : 1) : 0;
    public override string CompletedStageName
        => CompletedStage switch { 0 => "", 1 => "ResultReady", _ => "Invalidated" };
    public abstract Computed? UntypedComputed { get; }

    protected RpcInboundComputeCall(RpcInboundContext context, RpcMethodDef methodDef)
        : base(context, methodDef)
    {
        if (NoWait)
            throw Errors.InternalError($"{GetType().GetName()} is incompatible with NoWait option.");
    }

    // Protected & private methods

    public override Task? TryReprocess(int completedStage, CancellationToken cancellationToken)
    {
        lock (Lock) {
            var existingCall = Context.Peer.InboundCalls.Get(Id);
            if (existingCall != this || ResultTask == null)
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
            if (Context.Peer.Handshake is { ProtocolVersion: <= 1 } && UntypedComputed != null) {
                // '@' is required to make it compatible with pre-v7.2 versions
                var versionHeader = new RpcHeader(WellKnownRpcHeaders.Version, UntypedComputed.Version.FormatVersion('@'));
                ResultHeaders = ResultHeaders.WithOrReplace(versionHeader);
            }
            if (CallCancelToken.IsCancellationRequested) {
                // The call is cancelled by remote party
                UnregisterFromLock();
                return;
            }
        }
        await SendResult().ConfigureAwait(false);
        await ProcessStage2(cancellationToken).ConfigureAwait(false);
    }

    protected async Task ProcessStage2(CancellationToken cancellationToken)
    {
        var mustSendInvalidation = true;
        try {
            if (UntypedComputed is { } computed) {
                using var commonCts = cancellationToken.LinkWith(CallCancelToken);
                await computed.WhenInvalidated(commonCts.Token).ConfigureAwait(false);
            }
            else
                await TickSource.Default.WhenNextTick()
                    .ConfigureAwait(false); // A bit of extra delay in case there is no computed
        }
        catch (OperationCanceledException) when (CallCancelToken.IsCancellationRequested) {
            // The call is cancelled by remote party
            mustSendInvalidation = false;
        }
        Unregister();
        if (mustSendInvalidation) {
            var computeSystemCallSender = Hub.Services.GetRequiredService<RpcComputeSystemCallSender>();
            await computeSystemCallSender.Invalidate(Context.Peer, Id, ResultHeaders).ConfigureAwait(false);
        }
    }
}

public sealed class RpcInboundComputeCall<TResult>(RpcInboundContext context, RpcMethodDef methodDef)
    : RpcInboundComputeCall(context, methodDef)
{
    public Computed<TResult>? Computed { get; private set; }
    public override Computed? UntypedComputed => Computed;

#if NET5_0_OR_GREATER
    protected override async Task<TResult> InvokeTarget()
    {
        var ccs = Fusion.Computed.BeginCapture();
        try {
            return await ((Task<TResult>)base.InvokeTarget()).ConfigureAwait(false);
        }
        finally {
            var computed = ccs.Context.TryGetCaptured<TResult>();
            if (computed != null) {
                lock (Lock)
                    Computed ??= computed;
            }
            ccs.Dispose();
        }
    }
#else
    protected override Task InvokeTarget()
    {
        return Implementation();

        async Task<TResult> Implementation()
        {
            var ccs = Fusion.Computed.BeginCapture();
            try {
                return await ((Task<TResult>)base.InvokeTarget()).ConfigureAwait(false);
            }
            finally {
                var computed = ccs.Context.TryGetCaptured<TResult>();
                if (computed != null) {
                    lock (Lock)
                        Computed ??= computed;
                }
                ccs.Dispose();
            }
        }
    }

#endif

    protected override Task InvokeTarget(RpcInboundMiddlewares middlewares)
        => DefaultInvokeTarget<TResult>(middlewares);

    protected override Task SendResult()
        => DefaultSendResult((Task<TResult>?)ResultTask);
}
