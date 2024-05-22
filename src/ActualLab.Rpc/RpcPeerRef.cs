namespace ActualLab.Rpc;

public record RpcPeerRef(Symbol Key, bool IsServer = false, bool IsBackend = false)
{
    public const string NoneConnectionKindPrefix = "none:";
    public const string LocalConnectionKindPrefix = "none:";
    public static RpcPeerRef Default { get; set; } = NewClient("default");
    public static RpcPeerRef None { get; set; } = NewClient(NoneConnectionKindPrefix + "0");
    public static RpcPeerRef Local { get; set; } = NewClient(LocalConnectionKindPrefix + "0");

    public virtual CancellationToken GoneToken => default;
    public bool IsGone => GoneToken.IsCancellationRequested;
    public bool CanBeGone => GoneToken.CanBeCanceled;

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
        return key.StartsWith(NoneConnectionKindPrefix, StringComparison.Ordinal)
            ? RpcPeerConnectionKind.None
            : key.StartsWith(LocalConnectionKindPrefix, StringComparison.Ordinal)
                ? RpcPeerConnectionKind.Local
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
