#if !NETSTANDARD

using ActualLab.Fusion.Authentication.Controllers;
using ActualLab.Fusion.Server;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Extension methods for <see cref="FusionMvcWebServerBuilder"/> to register
/// MVC-based authentication controllers.
/// </summary>
public static class FusionMvcWebServerBuilderExt
{
    public static FusionMvcWebServerBuilder AddAuthControllers(this FusionMvcWebServerBuilder fusionMvcWebServer)
    {
        var services = fusionMvcWebServer.Services;
        // Register ServerAuthHelper and AuthController
        services.AddSingleton(_ => ServerAuthHelper.Options.Default);
        services.AddScoped(c => new ServerAuthHelper(c.GetRequiredService<ServerAuthHelper.Options>(), c));
        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        return fusionMvcWebServer;
    }
}

#endif
