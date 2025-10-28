using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcCallOptions(RpcPeer? peerOverride = null)
{
    [field: ThreadStatic]
    public static RpcCallOptions? Value { get; internal set; }

    public RpcPeer? PeerOverride { get; init; } = peerOverride;
    public bool AllowRerouting { get; init; }
    public bool AssumeConnected { get; init; }

    public static Scope Activate(RpcCallOptions value)
        => new(value);

    public static Scope Activate(RpcPeer peerOverride, bool allowRerouting = false, bool assumeConnected = false)
        => new(new(peerOverride) { AllowRerouting = false, AssumeConnected = assumeConnected});

    public static Scope Deactivate()
        => new(null);

    public static RpcCallOptions? Use(RpcOutboundContext context, out bool allowRerouting)
    {
        (var value, Value) = (Value, null);
        if (value is null) {
            allowRerouting = true;
            return null;
        }

        if (value.PeerOverride is not null)
            context.Peer = value.PeerOverride;
        context.AssumeConnected = value.AssumeConnected;
        allowRerouting = value.AllowRerouting;
        return value;
    }

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly RpcCallOptions? _oldValue;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly RpcCallOptions? Value;

        internal Scope(RpcCallOptions? value)
            : this(value, RpcCallOptions.Value)
        { }

        internal Scope(RpcCallOptions? value, RpcCallOptions? oldValue)
        {
            Value = value;
            _oldValue = oldValue;
            if (!ReferenceEquals(Value, _oldValue))
                RpcCallOptions.Value = Value;
        }

        public void Dispose()
        {
            if (ReferenceEquals(Value, _oldValue))
                return; // Default or no-op instance

            var value = RpcCallOptions.Value;
            if (value is not null && !ReferenceEquals(Value, value))
                throw Errors.RpcCallRouteOverrideChanged();

            RpcCallOptions.Value = _oldValue;
        }
    }

}
