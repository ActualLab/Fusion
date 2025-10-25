using System.Diagnostics;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
public partial class RpcPeerRef : IEquatable<RpcPeerRef>
{
    private string? _toStringCached;
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
    public virtual CancellationToken RerouteToken => default;
    public bool CanBeRerouted => RerouteToken.CanBeCanceled;
    public bool IsRerouted => RerouteToken.IsCancellationRequested;

    // Protected properties
    protected bool IsInitialized { get; set; }
    protected bool UseReferentialEquality { get; init; }
    protected int AddressHashCode {
        get {
            if (field != 0)
                return field;

            field = Address.GetOrdinalHashCode();
            if (field == 0)
                field = -1;
            return field;
        }
    }

    public override string ToString()
    {
        if (!IsRerouted)
            return Address;

        return _toStringCached ??= "<*>" + Address;
    }

    /// <summary>
    /// This method must be called for any RpcPeerRef instance before using it.
    /// </summary>
    public RpcPeerRef Initialize()
    {
        IsInitialized = true; // Required to access Address and Versions properties
        try {
            if (Address.IsNullOrEmpty())
                Address = RpcPeerRefAddress.Format(this);
            if (Versions.IsEmpty)
                Versions = RpcDefaults.GetVersions(IsBackend);
        }
        catch {
            // If initialization fails, reset the state to uninitialized
            IsInitialized = false;
            throw;
        }
        return this;
    }

    // WhenXxx

    public async Task WhenRerouted()
        => await TaskExt.NeverEnding(RerouteToken).SilentAwait(false);

    public Task WhenRerouted(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? WhenReroutedWithCancellationToken(cancellationToken)
            : WhenRerouted();

        async Task WhenReroutedWithCancellationToken(CancellationToken cancellationToken1) {
            using var commonCts = RerouteToken.LinkWith(cancellationToken1);
            await TaskExt.NeverEnding(commonCts.Token).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

#pragma warning disable MA0001

    // Equality: UseReferentialEquality determines whether it is referential or based on Address.
    // By default, UseReferentialEquality is false, meaning equality is based on Address.
    //
    // The equality of RpcServerPeerRef MUST BE based on the Address property,
    // so two RpcServerPeerRef instances with the same Address are considered equal.
    // This is necessary to make sure an RPC client can reconnect to exactly the same peer rather than a new one.
    // See RpcWebSocketServer.Invoke and RpcWebSocketServerPeerRefFactory implementations,
    // they use the 'clientId' parameter to construct a new RpcServerPeerRef on each WebSocket connection.
    //
    // The equality of RpcClientPeerRef CAN BE based on the Address property or referential.
    // Referential equality must be a preference when such refs are always cached / reused.

#pragma warning disable CA1307, CA1309, MA0001
    public bool Equals(RpcPeerRef? other)
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
        return obj is RpcPeerRef other
            && AddressHashCode == other.AddressHashCode
            && Address.Equals(other.Address);
#pragma warning restore CA1307, CA1309, MA0001
    }

    public sealed override int GetHashCode()
        => UseReferentialEquality
            ? RuntimeHelpers.GetHashCode(this)
            : AddressHashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RpcPeerRef? left, RpcPeerRef? right)
        => left is not null && left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RpcPeerRef? left, RpcPeerRef? right)
        => !(left is not null && left.Equals(right));

#pragma warning restore MA0001

    // Protected methods

    protected void ThrowIfUninitialized()
    {
        if (!IsInitialized)
            throw Errors.NotInitialized();
    }
}
