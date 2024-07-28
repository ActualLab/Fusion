using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

public class RpcInboundContext
{
    private static readonly AsyncLocal<RpcInboundContext?> CurrentLocal = new();

    public static RpcInboundContext? Current => CurrentLocal.Value;

    public readonly RpcPeer Peer;
    public readonly RpcMessage Message;
    public readonly CancellationToken CancellationToken;
    public readonly CpuTimestamp CreatedAt = CpuTimestamp.Now;
    public RpcInboundCall Call { get; protected init; }

    public static RpcInboundContext GetCurrent()
        => CurrentLocal.Value ?? throw Errors.NoCurrentRpcInboundContext();

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken cancellationToken)
        : this(peer, message, cancellationToken, true)
    { }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#pragma warning disable CA1068
    protected RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken cancellationToken, bool initializeCall)
#pragma warning restore CA1068
    {
        Peer = peer;
        Message = message;
        CancellationToken = cancellationToken;
        Call = initializeCall ? RpcInboundCall.New(message.CallTypeId, this, GetMethodDef()) : null!;
    }

    public Scope Activate()
        => new(this);

    // Nested types

    private RpcMethodDef? GetMethodDef()
    {
        var method = Peer.ServerMethodResolver[Message.Service, Message.Method];
        if (method == null || method.IsSystem)
            return method;

        return Peer.InboundCallFilter.Invoke(Peer, method) ? method : null;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly RpcInboundContext? _oldContext;

        public readonly RpcInboundContext Context;

        internal Scope(RpcInboundContext context)
        {
            Context = context;
            _oldContext = CurrentLocal.Value;
            TryActivate(context);
        }

        internal Scope(RpcInboundContext context, RpcInboundContext? oldContext)
        {
            Context = context;
            _oldContext = oldContext;
            TryActivate(context);
        }

        public void Dispose()
            => TryActivate(_oldContext);

        private void TryActivate(RpcInboundContext? context)
        {
            if (Context != _oldContext)
                CurrentLocal.Value = context;
        }
    }
}
