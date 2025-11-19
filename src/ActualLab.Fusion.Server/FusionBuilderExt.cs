using ActualLab.Fusion.Server.Internal;
using ActualLab.Rpc.Server;

namespace ActualLab.Fusion.Server;

public static class FusionBuilderExt
{
    static FusionBuilderExt()
        => FusionServerModuleInitializer.Touch();

    extension(FusionBuilder fusion)
    {
        public FusionWebServerBuilder AddWebServer()
            => new(fusion, null);

        public FusionWebServerBuilder AddWebServer(bool exposeBackend)
        {
            var webServer = new FusionWebServerBuilder(fusion, null);
            fusion.Rpc.AddWebSocketServer(true);
            return webServer;
        }

        public FusionBuilder AddWebServer(Action<FusionWebServerBuilder> configure)
            => new FusionWebServerBuilder(fusion, configure).Fusion;
    }
}
