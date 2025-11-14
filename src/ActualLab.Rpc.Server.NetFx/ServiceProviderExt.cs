using System.Web.Http.Dependencies;
using ActualLab.Rpc.Server.Internal;

namespace ActualLab.Rpc.Server;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        public IDependencyResolver ToDependencyResolver()
            => new DependencyResolver(services);
    }
}
