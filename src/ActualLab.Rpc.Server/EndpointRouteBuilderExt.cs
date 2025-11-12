using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

public static class EndpointRouteBuilderExt
{
    public static IEndpointRouteBuilder MapRpcWebSocketServer(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null)
            throw new ArgumentNullException(nameof(endpoints));

        var services = endpoints.ServiceProvider;
        var server = services.GetRequiredService<RpcWebSocketServer>();
        var options = server.Options;

        endpoints.Map(options.RequestPath, HandleRequest(isBackend: false));
        if (options.ExposeBackend)
            endpoints.Map(options.BackendRequestPath, HandleRequest(isBackend: true));
        return endpoints;

        RequestDelegate HandleRequest(bool isBackend)
            => httpContext => server.Invoke(httpContext, isBackend);
    }
}
