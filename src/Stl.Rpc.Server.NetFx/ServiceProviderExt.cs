using System.Web.Http.Dependencies;
using ActualLab.Rpc.Server.Internal;

namespace ActualLab.Rpc.Server;

public static class ServiceProviderExt
{
    public static IDependencyResolver ToDependencyResolver(this IServiceProvider services)
        => new DependencyResolver(services);
}
