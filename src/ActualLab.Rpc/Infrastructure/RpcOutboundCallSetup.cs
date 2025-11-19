using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;


public sealed class RpcOutboundCallSetup
{
    [field: ThreadStatic]
    public static RpcOutboundCallSetup? Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal set;
    }

    public RpcPeer? Peer { get; }
    public RpcRoutingMode RoutingMode { get; }
    public byte CallTypeId { get; init; } // You typically shouldn't set it!
    public RpcHeader[]? Headers { get; init; } // You typically shouldn't set it!
    public RpcCacheInfoCapture? CacheInfoCapture { get; init; }
    public RpcOutboundContext? ProducedContext { get; private set; } // Set by ProduceContext

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundCallSetup()
        => RoutingMode = RpcRoutingMode.Full;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundCallSetup(RpcPeer peer)
    {
        Peer = peer;
        RoutingMode = RpcRoutingMode.None;
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundCallSetup(RpcPeer peer, RpcRoutingMode routingMode)
    {
        if (routingMode is RpcRoutingMode.Full)
            throw new ArgumentOutOfRangeException(nameof(routingMode));
        Peer = peer;
        RoutingMode = routingMode;
    }

    // Static methods

    public static RpcOutboundContext ProduceContext()
    {
        (var value, Value) = (Value, null);
        if (value is null)
            return new RpcOutboundContext();

        var context = new RpcOutboundContext(value.Headers);
        if (value.Peer is not null)
            context.Peer = value.Peer;
        context.RoutingMode = value.RoutingMode;
        if (value.CacheInfoCapture is not null)
            context.CacheInfoCapture = value.CacheInfoCapture;
        value.ProducedContext = context;
        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope Activate()
        => new(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Deactivate()
        => new(value: null);

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly RpcOutboundCallSetup? _oldValue;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly RpcOutboundCallSetup? Value;

        internal Scope(RpcOutboundCallSetup? value)
            : this(value, RpcOutboundCallSetup.Value)
        { }

        internal Scope(RpcOutboundCallSetup? value, RpcOutboundCallSetup? oldValue)
        {
            Value = value;
            _oldValue = oldValue;
            if (!ReferenceEquals(Value, _oldValue))
                RpcOutboundCallSetup.Value = Value;
        }

        public void Dispose()
        {
            if (ReferenceEquals(Value, _oldValue))
                return; // Default or no-op instance

            var value = RpcOutboundCallSetup.Value;
            if (value is not null && !ReferenceEquals(Value, value))
                throw Errors.RpcOutboundCallSetupChanged();

            RpcOutboundCallSetup.Value = _oldValue;
        }
    }

}
