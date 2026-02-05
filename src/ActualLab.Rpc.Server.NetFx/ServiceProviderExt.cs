using System.Web.Http.Dependencies;
using ActualLab.Rpc.Server.Internal;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to create
/// <see cref="IDependencyResolver"/> instances for Web API.
/// </summary>
public static class ServiceProviderExt
{
    public static IDependencyResolver ToDependencyResolver(this IServiceProvider services)
        => new DependencyResolver(services);
}
