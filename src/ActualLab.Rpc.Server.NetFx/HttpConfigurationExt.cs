using System.Web.Http;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="HttpConfiguration"/> to configure
/// dependency resolution using <see cref="IServiceCollection"/>.
/// </summary>
public static class HttpConfigurationExt
{
    public static HttpConfiguration AddDependencyResolver(
        this HttpConfiguration httpConfiguration,
        Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        configureServices.Invoke(services);
        httpConfiguration.DependencyResolver = services.BuildServiceProvider().ToDependencyResolver();
        return httpConfiguration;
    }
}
