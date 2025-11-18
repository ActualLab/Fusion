using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed class RpcOutboundCallSetup(RpcPeer? peer = null)
{
    [field: ThreadStatic]
    public static RpcOutboundCallSetup? Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal set;
    }

    public byte CallTypeId { get; init; } // You typically shouldn't set it!
    public RpcHeader[]? Headers { get; init; } // You typically shouldn't set it!
    public RpcPeer? Peer { get; init; } = peer;
    public RpcCacheInfoCapture? CacheInfoCapture { get; init; }
    public RpcOutboundContext? ProducedContext { get; private set; } // Set by ProduceContext

    // Static methods

    public static RpcOutboundContext ProduceContext()
    {
        (var value, Value) = (Value, null);
        if (value is null)
            return new RpcOutboundContext();

        var context = new RpcOutboundContext(value.Headers);
        if (value.Peer is not null)
            context.Peer = value.Peer;
        context.IsPrerouted = value.Peer is not null;
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
