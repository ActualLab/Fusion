using ActualLab.Rpc;
using System.Collections.Concurrent;

namespace ActualLab.Fusion.Tests.Rpc;

public sealed class RpcTestPeerRef : RpcPeerRef
{
    private static readonly ConcurrentDictionary<string, LazySlim<string, RpcTestPeerRef>> Cache = new();

    public string HostId { get; }
    public override CancellationToken RerouteToken { get; }

    public static RpcTestPeerRef Get(string hostId)
    {
        var sw = new SpinWait();
        while (true) {
            var peerRef = Cache.GetOrAdd(hostId, static (hostId, lazy) => new RpcTestPeerRef(hostId, lazy));
            if (!peerRef.IsRerouted)
                return peerRef;

            sw.SpinOnce();
        }
    }

    private RpcTestPeerRef(string hostId, LazySlim<string, RpcTestPeerRef> lazy)
    {
        var meshState = TestMeshState.State.Value;
        HostId = hostId;
        HostInfo = $"test-{hostId}-v{meshState.Version}";
        UseReferentialEquality = true;

        var rerouteTokenSource = new CancellationTokenSource();
        RerouteToken = rerouteTokenSource.Token;
        _ = Task.Run(async () => {
            await TestMeshState.State.Computed
                .When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None)
                .ConfigureAwait(false);
            Cache.TryRemove(HostId, lazy);
            rerouteTokenSource.Cancel();
        }, CancellationToken.None);
        Initialize();
    }
}
