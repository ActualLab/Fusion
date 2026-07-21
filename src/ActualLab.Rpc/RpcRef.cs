using System.Diagnostics;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

/// <summary>
/// A stable reference to a logical RPC peer target, encapsulating its address,
/// connection kind, versioning info, and the current <see cref="RpcRoute"/> generation.
/// </summary>
[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
public partial class RpcRef : IEquatable<RpcRef>
{
    private volatile RpcRoute? _route;
#if NET9_0_OR_GREATER
    private readonly Lock _routeLock = new();
#else
    private readonly object _routeLock = new();
#endif
    private int _lastRouteVersion;
    private string DebugValue => IsInitialized ? ToString() : "<uninitialized>";

    public bool IsServer { get; init; }
    public bool IsBackend { get; init; }
    public RpcPeerConnectionKind ConnectionKind { get; init; } = RpcPeerConnectionKind.Remote;
    public string SerializationFormat { get; init; } = "";
    public string HostInfo { get; init; } = "";

    // Properties that require initialization
    public string Address {
        get { ThrowIfUninitialized(); return field; }
        set;
    } = "";

    public VersionSet Versions {
        get { ThrowIfUninitialized(); return field; }
        set;
    } = VersionSet.Empty;

    // Rerouting-related properties
    public RpcRoute Route {
        get {
            var route = _route ?? throw Errors.NotInitialized();
            if (!route.IsChanged) // Always false for static routes
                return route;

            lock (_routeLock) { // Double-check locking
                route = _route!;
                if (!route.IsChanged)
                    return route;

                route = CreateRoute();
                if (route.IsStatic)
                    throw Errors.InternalError(
                        $"{GetType().GetName()}.{nameof(CreateRoute)}() must not return a static route for a routed {nameof(RpcRef)}.");

                return _route = route;
            }
        }
    }

    // Protected properties
    protected bool IsInitialized { get; set; }
    protected bool UseReferentialEquality { get; init; }
    protected int AddressHashCode {
        get {
            if (field != 0)
                return field;

            if (!IsInitialized)
                throw Errors.NotInitialized();

            field = Address.GetOrdinalHashCode();
            if (field == 0)
                field = -1;
            return field;
        }
    }

    public override string ToString()
        => Address; // Stable; route generation info surfaces via RpcRoute.ToString() / RpcPeer.ToString()

    /// <summary>
    /// This method must be called for any RpcRef instance before using it.
    /// Subclasses must set every field <see cref="CreateRoute"/> depends on before calling it.
    /// </summary>
    public RpcRef Initialize()
    {
        IsInitialized = true; // Required to access Address and Versions properties
        try {
            if (Address.IsNullOrEmpty())
                Address = RpcRefAddress.Format(this);
            if (Versions.IsEmpty)
                Versions = RpcDefaults.GetVersions(IsBackend);
            // ReSharper disable once NonAtomicCompoundOperator
            _route ??= CreateRoute();
        }
        catch {
            // If initialization fails, reset the state to uninitialized
            IsInitialized = false;
            throw;
        }
        return this;
    }

    public RpcRoute Reset()
    {
        // Marks the current route as changed and eagerly mints the next generation
        var route = _route ?? throw Errors.NotInitialized();
        if (route.IsStatic)
            return route;

        route.MarkChanged();
        return Route;
    }

    // WhenXxx moved to RpcRoute

#pragma warning disable MA0001

    // Equality: UseReferentialEquality determines whether it is referential or based on Address.
    // By default, UseReferentialEquality is false, meaning equality is based on Address.
    //
    // The equality of server RpcRef-s MUST BE based on the Address property,
    // so two server RpcRef instances with the same Address are considered equal.
    // This is necessary to make sure an RPC client can reconnect to exactly the same peer rather than a new one.
    // See RpcWebSocketServer.Invoke and RpcWebSocketServerRefFactory implementations,
    // they use the 'clientId' parameter to construct a new server RpcRef on each WebSocket connection.
    //
    // The equality of client RpcRef-s CAN BE based on the Address property or referential.
    // Referential equality must be a preference when such refs are always cached / reused.

#pragma warning disable CA1307, CA1309, MA0001
    public bool Equals(RpcRef? other)
        => UseReferentialEquality
            ? ReferenceEquals(this, other)
            : other is not null && AddressHashCode == other.AddressHashCode && Address.Equals(other.Address);
#pragma warning restore CA1307, CA1309, MA0001

    public sealed override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;
        if (UseReferentialEquality)
            return false; // We already know the references are different

#pragma warning disable CA1307, CA1309, MA0001
        return obj is RpcRef other
            && AddressHashCode == other.AddressHashCode
            && Address.Equals(other.Address);
#pragma warning restore CA1307, CA1309, MA0001
    }

    public sealed override int GetHashCode()
        => UseReferentialEquality
            ? RuntimeHelpers.GetHashCode(this)
            : AddressHashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RpcRef? left, RpcRef? right)
        => ReferenceEquals(left, right) || left is not null && left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RpcRef? left, RpcRef? right)
        => !(left == right);

#pragma warning restore MA0001

    // Protected and internal methods

    protected virtual RpcRoute CreateRoute()
        => RpcRoute.NewStatic(this); // Static route = the ref never reroutes

    protected void ThrowIfUninitialized()
    {
        if (!IsInitialized)
            throw Errors.NotInitialized();
    }

    internal int NextRouteVersion()
        => Interlocked.Increment(ref _lastRouteVersion);
}
