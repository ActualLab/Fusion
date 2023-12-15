using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcBuilder AddRpc(this IServiceCollection services)
        => new(services, null);

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IServiceCollection AddRpc(this IServiceCollection services, Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure).Services;
}
