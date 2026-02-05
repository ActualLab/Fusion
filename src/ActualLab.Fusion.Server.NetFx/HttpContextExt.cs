using System.Web.Http.Controllers;
using System.Web.Http.Dependencies;

namespace ActualLab.Fusion.Server;

/// <summary>
/// Extension methods for <see cref="IDependencyScope"/> and
/// <see cref="HttpActionContext"/> to simplify service resolution in Web API.
/// </summary>
public static class HttpContextExt
{
    public static T GetRequiredService<T>(this IDependencyScope dependencyScope)
    {
        var service = GetService<T>(dependencyScope);
        if (service is null)
            throw new InvalidOperationException($"Required service '{typeof(T)}' is not registered.");
        return service;
    }

    public static T GetService<T>(this IDependencyScope dependencyScope)
    {
        var service = (T)dependencyScope.GetService(typeof(T));
        return service;
    }

    public static IDependencyScope GetAppServices(this HttpActionContext httpContext)
    {
        var appServices = httpContext.RequestContext.Configuration.DependencyResolver;
        return appServices;
    }
}
