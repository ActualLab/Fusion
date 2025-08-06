using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcPeerRef(Symbol id) : IEquatable<RpcPeerRef>, IHasId<Symbol>
{
    // We want this class to be slim (until parsed), but equatable.
    // That's why it has just two fields: Parsed and Id.
    [field: AllowNull, MaybeNull]
    protected ParsedRpcPeerRef Parsed => field ??= Parser.Invoke(Id.Value);

    public Symbol Id { get; } = id;
    public bool IsServer => Parsed.IsServer;
    public bool IsBackend => Parsed.IsBackend;
    public string SerializationFormat => Parsed.SerializationFormat;
    public RpcPeerConnectionKind ConnectionKind => Parsed.ConnectionKind;
    public VersionSet Versions => Parsed.Versions;
    public string Data => Parsed.Data;
    public virtual CancellationToken RerouteToken => default;

    public bool CanBeRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.CanBeCanceled;
    }

    public bool IsRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.IsCancellationRequested;
    }

    public RpcPeerRef(ParsedRpcPeerRef parsed)
        : this(parsed.Id)
        => Parsed = parsed;

    public override string ToString()
        => IsRerouted
            ? "<*>" + Id.Value
            : Id.Value;

    // WhenXxx

    public async Task WhenRerouted()
        => await TaskExt.NewNeverEndingUnreferenced().WaitAsync(RerouteToken).SilentAwait(false);

    public Task WhenRerouted(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? WhenReroutedWithCancellationToken(cancellationToken)
            : WhenRerouted();

        async Task WhenReroutedWithCancellationToken(CancellationToken cancellationToken1) {
            using var tcs = RerouteToken.LinkWith(cancellationToken1);
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(tcs.Token).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    // Equality

    public bool Equals(RpcPeerRef? other)
        => ReferenceEquals(this, other) || (other is not null && Id.Equals(other.Id));

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;

        return Equals((RpcPeerRef)obj);
    }

    public override int GetHashCode()
        => Id.GetHashCode();
}
