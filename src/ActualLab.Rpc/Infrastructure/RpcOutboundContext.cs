using System.Diagnostics;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

/// <summary>
/// Encapsulates the context for sending an outbound RPC call, including headers, routing, and caching.
/// </summary>
public sealed class RpcOutboundContext(RpcHeader[]? headers = null)
{
    [ThreadStatic] private static RpcOutboundContext? _current;

    public static RpcOutboundContext? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _current;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] set => _current = value;
    }

    public RpcHeader[]? Headers = headers;
    public RpcMethodDef? MethodDef;
    public ArgumentList? Arguments;
    public CancellationToken CancellationToken; // From Arguments
    public RpcOutboundCall? Call;
    public RpcPeer? Peer;
    public RpcRoutingMode RoutingMode;
    public long RelatedId;
    public RpcCacheInfoCapture? CacheInfoCapture;
    public RpcInboundCall? InboundCall;
    public Exception? InboundCallTraceError;
    public RpcOutboundCallTrace? Trace;
    public ActivityContext ActivityContext;
    public CpuTimestamp? MetricsStartedAt;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundContext(RpcPeer peer, RpcHeader[]? headers = null)
        : this(headers)
    {
        Peer = peer;
        RoutingMode = RpcRoutingMode.Prerouted;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundContext(RpcPeer peer, long relatedId, RpcHeader[]? headers = null)
        : this(headers)
    {
        Peer = peer;
        RoutingMode = RpcRoutingMode.Prerouted;
        RelatedId = relatedId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope Activate()
        => new(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Deactivate()
        => new(null!);

    public RpcOutboundCall? PrepareCall(RpcMethodDef methodDef, ArgumentList arguments)
    {
        if (MethodDef != methodDef) {
            if (MethodDef is not null)
                throw ActualLab.Internal.Errors.AlreadyInvoked(nameof(PrepareCall));

            // MethodDef, Arguments, CancellationToken
            MethodDef = methodDef;
            Arguments = arguments;
            var ctIndex = methodDef.CancellationTokenIndex;
            CancellationToken = ctIndex >= 0 ? arguments.GetCancellationToken(ctIndex) : default;
        }

        // Peer & Call
        var hub = MethodDef.Hub;
        if (CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly }) {
            Peer ??= hub.LoopbackPeer; // Peer must be set, but invoking the router makes no sense here
            Call = methodDef.CreateOutboundCall(this);
            return Call ?? throw ActualLab.Internal.Errors.InternalError("Call is null, which isn't expected here.");
        }

        Peer = methodDef.RouteCall(arguments, RoutingMode, Peer);
        Call = methodDef.CreateOutboundCall(this);
        if (Call is null)
            return Call;

        ActivityContext = Activity.Current?.Context ?? default;
        if (RpcInstruments.IsOutboundEnabled)
            MetricsStartedAt ??= CpuTimestamp.Now;
        if (MethodDef.Tracer is { } tracer)
            Trace ??= tracer.StartOutboundTrace(Call);
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
            CancellationToken = ctIndex >= 0 ? arguments.GetCancellationToken(ctIndex) : default;
        }

        // Peer & Call
        if (Peer is null)
            throw ActualLab.Internal.Errors.InternalError(
                $"Peer is null, which isn't expected in {nameof(PrepareCallForSendNoWait)}.");
        if (RoutingMode is not RpcRoutingMode.Prerouted)
            throw ActualLab.Internal.Errors.InternalError(
                $"RoutingMode is not {nameof(RoutingMode.Prerouted)}, which isn't expected in {nameof(PrepareCallForSendNoWait)}.");
        Call = methodDef.CreateOutboundCall(this);
        return Call;
    }

    public RpcOutboundCall? PrepareReroutedCall()
    {
        if (MethodDef is null || Arguments is null)
            throw ActualLab.Internal.Errors.NotInvoked(nameof(PrepareCall));

        // Peer & Call
        var oldPeer = Peer;
        if (oldPeer is null || !oldPeer.Route.IsStatic)
            Peer = MethodDef.RouteOutboundCall(Arguments);
        Call = MethodDef.CreateOutboundCall(this);
        Trace = Call is not null && MethodDef.Tracer is { } tracer
            ? tracer.StartOutboundTrace(Call)
            : null;
        if (ReferenceEquals(oldPeer, Peer))
            Peer?.Log.LogWarning("The call {Call} is rerouted to the same peer: {Peer}", Call, Peer);
        return Call;
    }

    public void CompleteMetrics(RpcOutboundCall call)
    {
        if (MetricsStartedAt is not { } startedAt)
            return;

        var error = call.ResultTask.ToResultSynchronously().Error;
        if (error is RpcRerouteException)
            return;

        MetricsStartedAt = null;
        RpcInstruments.RegisterOutboundCall(MethodDef!, startedAt.Elapsed.TotalMilliseconds, error);
    }

    // Nested types

    /// <summary>
    /// RAII scope that saves and restores the ambient <see cref="RpcOutboundContext"/> on disposal.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly RpcOutboundContext? _oldContext;

        public readonly RpcOutboundContext Context;

        internal Scope(RpcOutboundContext context)
            : this(context, _current)
        { }

        internal Scope(RpcOutboundContext context, RpcOutboundContext? oldContext)
        {
            Context = context;
            _oldContext = oldContext;
            if (!ReferenceEquals(Context, _oldContext))
                _current = Context;
        }

        public void Dispose()
        {
            if (ReferenceEquals(Context, _oldContext))
                return; // Default or no-op instance

            if (!ReferenceEquals(Context, _current))
                throw Errors.RpcOutboundContextChanged();

            _current = _oldContext;
        }
    }
}
