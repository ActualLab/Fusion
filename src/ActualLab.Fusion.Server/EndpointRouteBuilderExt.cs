using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ActualLab.Fusion.Server.Endpoints;

namespace ActualLab.Fusion.Server;

#if NET7_0_OR_GREATER

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> to map Fusion render mode endpoints.
/// </summary>
public static class EndpointRouteBuilderExt
{
    public static IEndpointRouteBuilder MapFusionRenderModeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var handler = services.GetRequiredService<RenderModeEndpoint>();
        endpoints
            .MapGet("/fusion/renderMode", handler.Invoke)
            .WithGroupName("FusionRenderMode");
        endpoints
            .MapGet("/fusion/renderMode/{renderMode}", handler.Invoke)
            .WithGroupName("FusionRenderMode");
        return endpoints;
    }
}

#endif
