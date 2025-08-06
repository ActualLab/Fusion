using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcPeerRef(Symbol key) : IEquatable<RpcPeerRef>, IHasId<Symbol>
{
    [field: AllowNull, MaybeNull]
    protected ParsedRpcPeerRef Parsed => field ??= Parse(Key.Value);

    Symbol IHasId<Symbol>.Id => Key;
    public Symbol Key { get; } = key;

    public virtual CancellationToken RerouteToken => default;

    public bool CanBeRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.CanBeCanceled;
    }

    public bool IsRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.IsCancellationRequested;
    }

    public bool IsServer => Parsed.IsServer;
    public bool IsBackend => Parsed.IsBackend;
    public string SerializationFormatKey => Parsed.SerializationFormatKey;
    public RpcPeerConnectionKind ConnectionKind => Parsed.ConnectionKind;
    public VersionSet Versions => Parsed.Versions;

    public RpcPeerRef(ParsedRpcPeerRef parsed)
        : this(parsed.Key)
        => Parsed = parsed;

    public override string ToString()
    {
        var result = Key.Value;
        if (IsRerouted)
            result = "[rerouted]" + result;
        return result;
    }

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
        => ReferenceEquals(this, other) || (other is not null && Key.Equals(other.Key));

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
        => Key.GetHashCode();
}
