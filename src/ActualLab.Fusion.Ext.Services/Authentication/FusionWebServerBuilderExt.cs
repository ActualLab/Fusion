#if !NETSTANDARD

using ActualLab.Fusion.Authentication.Endpoints;
using ActualLab.Fusion.Server;

namespace ActualLab.Fusion.Authentication;

public static class FusionWebServerBuilderExt
{
    public static FusionWebServerBuilder AddAuthEndpoints(this FusionWebServerBuilder fusionWebServer)
    {
        var services = fusionWebServer.Services;
        // Register ServerAuthHelper and AuthEndpoints
        services.AddSingleton(_ => ServerAuthHelper.Options.Default);
        services.AddScoped(c => new ServerAuthHelper(c.GetRequiredService<ServerAuthHelper.Options>(), c));
        services.AddSingleton(_ => AuthEndpoints.Options.Default);
        services.AddSingleton(c => new AuthEndpoints(c.GetRequiredService<AuthEndpoints.Options>()));
        return fusionWebServer;
    }

    public static FusionWebServerBuilder ConfigureServerAuthHelper(
        this FusionWebServerBuilder fusionWebServer,
        Func<IServiceProvider, ServerAuthHelper.Options> optionsFactory)
    {
        fusionWebServer.Services.AddSingleton(optionsFactory);
        return fusionWebServer;
    }

    public static FusionWebServerBuilder ConfigureAuthEndpoint(
        this FusionWebServerBuilder fusionWebServer,
        Func<IServiceProvider, AuthEndpoints.Options> optionsFactory)
    {
        fusionWebServer.Services.AddSingleton(optionsFactory);
        return fusionWebServer;
    }
}

#endif
