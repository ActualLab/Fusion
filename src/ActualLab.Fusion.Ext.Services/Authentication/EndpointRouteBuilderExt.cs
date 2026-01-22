#if NET7_0_OR_GREATER
using ActualLab.Fusion.Authentication.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ActualLab.Fusion.Authentication;

public static class EndpointRouteBuilderAuthExt
{
    public static IEndpointRouteBuilder MapFusionAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var handler = services.GetRequiredService<AuthEndpoints>();
        endpoints
            .MapGet("/signIn", handler.SignIn)
            .WithGroupName("FusionAuth");
        endpoints
            .MapGet("/signIn/{scheme}", handler.SignIn)
            .WithGroupName("FusionAuth");
        endpoints
            .MapGet("/signOut", handler.SignOut)
            .WithGroupName("FusionAuth");
        return endpoints;
    }
}
#endif
