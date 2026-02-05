using Microsoft.AspNetCore.Builder;
using ActualLab.Fusion.Server.Middlewares;

namespace ActualLab.Fusion.Server;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to configure Fusion middleware.
/// </summary>
public static class ApplicationBuilderExt
{
    public static IApplicationBuilder UseFusionSession(this IApplicationBuilder app)
        => app.UseMiddleware<SessionMiddleware>();
}
