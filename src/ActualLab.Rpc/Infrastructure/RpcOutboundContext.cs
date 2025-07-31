using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

public sealed class RpcOutboundContext(byte callTypeId, RpcHeader[]? headers = null)
{
    [ThreadStatic] private static RpcOutboundContext? _current;

    public static RpcOutboundContext? Current => _current;

    public byte CallTypeId = callTypeId;
    public RpcHeader[]? Headers = headers;
    public RpcMethodDef? MethodDef;
    public ArgumentList? Arguments;
    public CancellationToken CallCancelToken;
    public RpcOutboundCall? Call;
    public RpcPeer? Peer;
    public long RelatedId;
    public int SizeHint;
    public RpcCacheInfoCapture? CacheInfoCapture;
    public RpcOutboundCallTrace? Trace;

    public static RpcOutboundContext GetCurrent()
        => Current ?? throw Errors.NoCurrentRpcOutboundContext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundContext(RpcHeader[]? headers = null)
        : this(0, headers)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundContext(RpcPeer peer, RpcHeader[]? headers = null)
        : this(0, headers)
        => Peer = peer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundContext(RpcPeer peer, long relatedId, RpcHeader[]? headers = null)
        : this(0, headers)
    {
        Peer = peer;
        RelatedId = relatedId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope Activate()
        => new(this, _current);

    public RpcOutboundCall? PrepareCall(RpcMethodDef methodDef, ArgumentList arguments)
    {
        if (MethodDef != methodDef) {
            if (MethodDef is not null)
                throw ActualLab.Internal.Errors.AlreadyInvoked(nameof(PrepareCall));

            // MethodDef, Arguments, CancellationToken
            MethodDef = methodDef;
            Arguments = arguments;
            var ctIndex = methodDef.CancellationTokenIndex;
            CallCancelToken = ctIndex >= 0 ? arguments.GetCancellationToken(ctIndex) : default;
        }

        // Peer & Call
        var hub = MethodDef.Hub;
        if (CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly }) {
            Peer ??= hub.LoopbackPeer; // Peer must be set, but invoking the router makes no sense here
            Call = RpcOutboundCall.New(this);
            return Call ?? throw ActualLab.Internal.Errors.InternalError("Call is null, which isn't expected here.");
        }

        Peer ??= hub.SafeCallRouter.Invoke(methodDef, arguments);
        Call = RpcOutboundCall.New(this);
        if (Call is not null) {
            if (MethodDef.Tracer is { } tracer)
                Trace ??= tracer.StartOutboundTrace(Call);
            hub.OutboundMiddlewares.NullIfEmpty()?.OnPrepareCall(this, false);
        }
        return Call;
    }

    public RpcOutboundCall? PrepareCallForSendNoWait(RpcMethodDef methodDef, ArgumentList arguments)
    {
        if (MethodDef != methodDef) {
            if (MethodDef is not null)
                throw ActualLab.Internal.Errors.AlreadyInvoked(nameof(PrepareCall));

            // MethodDef, Arguments, CancellationToken
            MethodDef = methodDef;
            Arguments = arguments;
            var ctIndex = methodDef.CancellationTokenIndex;
            CallCancelToken = ctIndex >= 0 ? arguments.GetCancellationToken(ctIndex) : default;
        }

        // Peer & Call
        var hub = MethodDef.Hub;
        Peer ??= hub.SafeCallRouter.Invoke(methodDef, arguments);
        Call = RpcOutboundCall.New(this);
        return Call;
    }

    public RpcOutboundCall? PrepareReroutedCall()
    {
        if (MethodDef is null || Arguments is null)
            throw ActualLab.Internal.Errors.NotInvoked(nameof(PrepareCall));

        // Peer & Call
        var hub = MethodDef.Hub;
        var oldPeer = Peer;
        Peer = hub.SafeCallRouter.Invoke(MethodDef, Arguments);
        Call = RpcOutboundCall.New(this);
        if (Call is not null) {
            // We don't start trace here, coz it's either started already or was sampled out
            hub.OutboundMiddlewares.NullIfEmpty()?.OnPrepareCall(this, true);
        }
        if (ReferenceEquals(oldPeer, Peer))
            Peer.Log.LogWarning("The call {Call} is rerouted to the same peer {Peer}", Call, Peer);
        return Call;
    }

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly RpcOutboundContext? _oldContext;

        public readonly RpcOutboundContext Context;

        internal Scope(RpcOutboundContext context, RpcOutboundContext? oldContext)
        {
            Context = context;
            _oldContext = oldContext;
            if (Context != _oldContext)
                _current = context;
        }

        public void Dispose()
        {
            if (Context != _current)
                throw Errors.RpcOutboundContextChanged();

            if (Context != _oldContext)
                _current = _oldContext;
        }
    }
}
