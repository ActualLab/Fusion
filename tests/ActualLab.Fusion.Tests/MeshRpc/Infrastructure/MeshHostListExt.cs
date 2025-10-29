namespace ActualLab.Fusion.Tests.MeshRpc;

public static class MeshHostListExt
{
    public static MeshHost? GetHostByRouteKey(this IReadOnlyList<MeshHost> hosts, int index)
        => hosts.Count != 0
            ? hosts[index.PositiveModulo(hosts.Count)]
            : null;
}
