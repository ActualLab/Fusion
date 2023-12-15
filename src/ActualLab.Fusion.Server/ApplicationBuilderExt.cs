using Microsoft.AspNetCore.Builder;
using ActualLab.Fusion.Server.Middlewares;

namespace ActualLab.Fusion.Server;

public static class ApplicationBuilderExt
{
    public static IApplicationBuilder UseFusionSession(this IApplicationBuilder app)
        => app.UseMiddleware<SessionMiddleware>();
}
