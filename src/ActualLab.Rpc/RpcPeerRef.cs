namespace ActualLab.Rpc;

public partial record RpcPeerRef(Symbol Key, bool IsServer = false, bool IsBackend = false)
{
    // private static readonly CancellationTokenSource FakeGoneCts = new();
    public virtual CancellationToken RerouteToken => default;

    public bool CanBeRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.CanBeCanceled;
    }

    public bool IsRerouted {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RerouteToken.IsCancellationRequested;
    }

    public static RpcPeerRef NewServer(Symbol key, bool isBackend = false)
        => new(key, true, isBackend);
    public static RpcPeerRef NewClient(Symbol key, bool isBackend = false)
        => new(key, false, isBackend);

    public override string ToString()
    {
        var result = $"{(IsBackend ? "backend-" : "")}{(IsServer ? "server" : "client")}:{Key}";
        if (IsRerouted)
            result = "[gone]" + result;
        return result;
    }

    public virtual VersionSet GetVersions()
        => IsBackend ? RpcDefaults.BackendPeerVersions : RpcDefaults.ApiPeerVersions;

    public virtual RpcPeerConnectionKind GetConnectionKind(RpcHub hub)
    {
        var key = Key.Value;
        return key.StartsWith(LocalCallPrefix, StringComparison.Ordinal)
            ? RpcPeerConnectionKind.LocalCall
            : key.StartsWith(LoopbackPrefix, StringComparison.Ordinal)
                ? RpcPeerConnectionKind.Loopback
                : RpcPeerConnectionKind.Remote;
    }

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
}
