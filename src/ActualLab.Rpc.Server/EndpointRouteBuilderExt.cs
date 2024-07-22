using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

public static class EndpointRouteBuilderExt
{
    public static IEndpointRouteBuilder MapRpcWebSocketServer(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var server = services.GetRequiredService<RpcWebSocketServer>();
        var settings = server.Settings;

        endpoints.MapGet(server.Settings.RequestPath, HandleRequest(false));
        if (settings.ExposeBackend)
            endpoints.MapGet(server.Settings.BackendRequestPath, HandleRequest(true));
        return endpoints;

        RequestDelegate HandleRequest(bool isBackend)
            => httpContext => server.Invoke(httpContext, isBackend);
    }
}
