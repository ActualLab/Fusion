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
            : key.StartsWith(LocalChannelPrefix, StringComparison.Ordinal)
                ? RpcPeerConnectionKind.LocalChannel
                : RpcPeerConnectionKind.Remote;
    }

    public async Task WhenGone(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Unit>();
        var r1 = cancellationToken.Register(static x => ((TaskCompletionSource<Unit>)x!).TrySetResult(default), tcs);
        var r2 = RerouteToken.Register(static x => ((TaskCompletionSource<Unit>)x!).TrySetResult(default), tcs);
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
}
