using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Client.Internal;

public interface IRpcInboundComputeCall;

public class RpcInboundComputeCall<TResult> : RpcInboundCall<TResult>, IRpcInboundComputeCall
{
    private CancellationTokenSource? _stopCompletionSource;

    protected override string DebugTypeName => "<=";

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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override Task CompleteAndSendResult()
    {
        Computed<TResult>? computed;
        CancellationToken stopCompletionToken;
        lock (Lock) {
            // 0. Complete trace
            if (Trace is { } trace) {
                trace.Complete(this);
                Trace = null;
            }

            // 1. Check if we even need to do any work here
            if (CancellationToken.IsCancellationRequested) {
                Unregister();
                return Task.CompletedTask;
            }

            // 2. Cancel already running completion first
            _stopCompletionSource.CancelAndDisposeSilently();
            var stopCompletionSource = CancellationToken.CreateLinkedTokenSource();
            stopCompletionToken = stopCompletionSource.Token;
            _stopCompletionSource = stopCompletionSource;

            // 3. Retrieve Computed + update ResultHeaders
            computed = Computed;
            if (computed != null) {
                // '@' is required to make it compatible with pre-v7.2 versions
                var versionHeader = new RpcHeader(FusionRpcHeaderNames.Version, computed.Version.FormatVersion('@'));
                ResultHeaders = ResultHeaders.With(versionHeader);
            }
        }

        // 4. Actually run completion
        return CompleteAsync();

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        async Task CompleteAsync() {
            var mustUnregister = false;
            try {
                await SendResult().WaitAsync(stopCompletionToken).ConfigureAwait(false);
                if (computed != null) {
                    await computed.WhenInvalidated(stopCompletionToken).ConfigureAwait(false);
                    await SendInvalidation().ConfigureAwait(false);
                }
                mustUnregister = true;
            }
            finally {
                if (mustUnregister || CancellationToken.IsCancellationRequested)
                    Unregister();
            }
        }
    }

    protected override bool Unregister()
    {
        lock (Lock) {
            if (!Context.Peer.InboundCalls.Unregister(this))
                return false; // Already completed or NoWait

            CancellationTokenSource.DisposeSilently();
            _stopCompletionSource.DisposeSilently();
        }
        return true;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private Task SendInvalidation()
    {
        var computeSystemCallSender = Hub.Services.GetRequiredService<RpcComputeSystemCallSender>();
        return computeSystemCallSender.Invalidate(Context.Peer, Id, ResultHeaders);
    }
}
