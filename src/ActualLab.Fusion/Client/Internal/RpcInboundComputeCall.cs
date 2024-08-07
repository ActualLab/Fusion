using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Client.Internal;

#pragma warning disable RCS1210, MA0022

public interface IRpcInboundComputeCall;

public class RpcInboundComputeCall<TResult> : RpcInboundCall<TResult>, IRpcInboundComputeCall
{
    public override string DebugTypeName => "<=";
    public override int CompletedStage
        => UntypedResultTask is { IsCompleted: true } ? (Computed is { } c && c.IsInvalidated() ? 2 : 1) : 0;
    public override string CompletedStageName
        => CompletedStage switch { 0 => "", 1 => "ResultReady", _ => "Invalidated" };

    public Computed<TResult>? Computed { get; protected set; }

    public RpcInboundComputeCall(RpcInboundContext context, RpcMethodDef methodDef)
        : base(context, methodDef)
    {
        if (NoWait)
            throw Errors.InternalError($"{GetType().GetName()} is incompatible with NoWait option.");
    }

    // Protected & private methods

    protected override async Task<TResult> InvokeTarget()
    {
        var ccs = Fusion.Computed.BeginCapture();
        try {
            return await base.InvokeTarget().ConfigureAwait(false);
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

    public override Task? TryReprocess(int completedStage, CancellationToken cancellationToken)
    {
        lock (Lock) {
            var existingCall = Context.Peer.InboundCalls.Get(Id);
            if (existingCall != this || ResultTask == null)
                return null;
        }

        return completedStage switch {
            >= 2 => Task.CompletedTask,
            1 => ProcessStage2(cancellationToken),
            _ => ProcessStage1(cancellationToken)
        };
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override async Task ProcessStage1(CancellationToken cancellationToken)
    {
        await ResultTask!.SilentAwait(false);
        lock (Lock) {
            if (Trace is { } trace) {
                trace.Complete(this);
                Trace = null;
            }
            if (Computed != null) {
                // '@' is required to make it compatible with pre-v7.2 versions
                var versionHeader = new RpcHeader(FusionRpcHeaderNames.Version, Computed.Version.FormatVersion('@'));
                ResultHeaders = ResultHeaders.WithOrReplace(versionHeader);
            }
        }
        if (CallCancelToken.IsCancellationRequested) {
            // The call is cancelled by remote party
            Unregister();
            return;
        }
        await SendResult().ConfigureAwait(false);
        await ProcessStage2(cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected async Task ProcessStage2(CancellationToken cancellationToken)
    {
        try {
            if (Computed is { } computed) {
                using var commonCts = cancellationToken.LinkWith(CallCancelToken);
                await computed.WhenInvalidated(commonCts.Token).ConfigureAwait(false);
            }
            else
                await TickSource.Default.WhenNextTick()
                    .ConfigureAwait(false); // A bit of extra delay in case there is no computed
        }
        catch (OperationCanceledException) when (CallCancelToken.IsCancellationRequested) {
            // The call is cancelled by remote party
            Unregister();
            return;
        }
        Unregister();
        await SendInvalidation().ConfigureAwait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private Task SendInvalidation()
    {
        var computeSystemCallSender = Hub.Services.GetRequiredService<RpcComputeSystemCallSender>();
        return computeSystemCallSender.Invalidate(Context.Peer, Id, ResultHeaders);
    }
}
