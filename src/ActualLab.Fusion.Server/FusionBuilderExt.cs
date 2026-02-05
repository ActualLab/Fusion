using ActualLab.Fusion.Server.Internal;
using ActualLab.Rpc.Server;

namespace ActualLab.Fusion.Server;

/// <summary>
/// Extension methods for <see cref="FusionBuilder"/> to add Fusion web server services.
/// </summary>
public static class FusionBuilderExt
{
    static FusionBuilderExt()
        => FusionServerModuleInitializer.Touch();

    public static FusionWebServerBuilder AddWebServer(this FusionBuilder fusion)
        => new(fusion, null);

    public static FusionWebServerBuilder AddWebServer(this FusionBuilder fusion, bool exposeBackend)
    {
        var webServer = new FusionWebServerBuilder(fusion, null);
        fusion.Rpc.AddWebSocketServer(true);
        return webServer;
    }

    public static FusionBuilder AddWebServer(this FusionBuilder fusion, Action<FusionWebServerBuilder> configure)
        => new FusionWebServerBuilder(fusion, configure).Fusion;
}
