namespace ActualLab.Fusion.Tests.MeshRpc;

public static class MeshHostListExt
{
    public static MeshHost? GetHostByShardIndex(this IReadOnlyList<MeshHost> hosts, int shardIndex)
        => hosts.Count != 0
            ? hosts[shardIndex.PositiveModulo(hosts.Count)]
            : null;
}
