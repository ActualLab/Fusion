namespace ActualLab.Rpc;

public partial record RpcPeerRef(Symbol Key, bool IsServer = false, bool IsBackend = false)
{
    // private static readonly CancellationTokenSource FakeGoneCts = new();
    public virtual CancellationToken GoneToken => default;

    public bool IsGone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GoneToken.IsCancellationRequested;
    }

    public bool CanBeGone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GoneToken.CanBeCanceled;
    }

    public static RpcPeerRef NewServer(Symbol key, bool isBackend = false)
        => new(key, true, isBackend);
    public static RpcPeerRef NewClient(Symbol key, bool isBackend = false)
        => new(key, false, isBackend);

    public override string ToString()
    {
        var result = $"{(IsBackend ? "backend-" : "")}{(IsServer ? "server" : "client")}:{Key}";
        if (IsGone)
            result = "[gone]" + result;
        return result;
    }

    public virtual VersionSet GetVersions()
        => IsBackend ? RpcDefaults.BackendPeerVersions : RpcDefaults.ApiPeerVersions;

    public virtual RpcPeerConnectionKind GetConnectionKind()
    {
        var key = Key.Value;
        return key.StartsWith(LocalCallPrefix, StringComparison.Ordinal)
            ? RpcPeerConnectionKind.LocalCall
            : key.StartsWith(LocalChannelPrefix, StringComparison.Ordinal)
                ? RpcPeerConnectionKind.LocalChannel
                : RpcPeerConnectionKind.Remote;
    }

    public async Task WhenGone(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource();
        var r1 = cancellationToken.Register(static x => ((TaskCompletionSource)x!).TrySetResult(), tcs);
        var r2 = GoneToken.Register(static x => ((TaskCompletionSource)x!).TrySetResult(), tcs);
        try {
            await tcs.Task.ConfigureAwait(false);
        }
        finally {
            // ReSharper disable once MethodHasAsyncOverload
            r2.Dispose();
            // ReSharper disable once MethodHasAsyncOverload
            r1.Dispose();
        }
    }

    // Operators

    public static implicit operator RpcPeerRef(RpcPeer peer) => peer.Ref;
}
