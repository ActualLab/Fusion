using ActualLab.Rpc.Server;

namespace ActualLab.Fusion.Server;

public static class FusionBuilderExt
{
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
