namespace ActualLab.Rpc;

public partial record RpcPeerRef(
    string Key,
    bool IsServer = false,
    bool IsBackend = false)
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

    public override string ToString()
    {
        var result = $"{(IsBackend ? "backend-" : "")}{(IsServer ? "server" : "client")}:{Key}";
        if (IsRerouted)
            result = "[gone]" + result;
        return result;
    }

    public string GetSerializationFormatKey()
    {
        var delimiterIndex = Key.LastIndexOf('$');
        return delimiterIndex >= 0
            ? Key.Substring(delimiterIndex + 1)
            : "";
    }

    public virtual RpcPeerConnectionKind GetConnectionKind(RpcHub hub)
    {
        return Key.StartsWith(LocalKeyPrefix, StringComparison.Ordinal)
            ? RpcPeerConnectionKind.Local
            : Key.StartsWith(LoopbackKeyPrefix, StringComparison.Ordinal)
                ? RpcPeerConnectionKind.Loopback
                : RpcPeerConnectionKind.Remote;
    }

    public virtual VersionSet GetVersions()
        => IsBackend ? RpcDefaults.BackendPeerVersions : RpcDefaults.ApiPeerVersions;

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
