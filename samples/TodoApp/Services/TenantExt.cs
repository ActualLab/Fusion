using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Server.Middlewares;
using Microsoft.AspNetCore.Http;

namespace Templates.TodoApp.Services;

public static class TenantExt
{
    public const string TagName = "t";

    public static DbShard GetTenant(this Session session)
        => DbShard.Parse(session.GetTag(TagName));

    public static DbShard GetTenant(this string folder)
    {
        var slashIndex = folder.IndexOf('/');
        if (slashIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(folder));

        return DbShard.Parse(folder[..slashIndex]);
    }

    public static Func<HttpContext, string> CreateTagExtractor(int tenant0Port, int tenantCount, int? ownPort)
    {
        int PortExtractor(HttpContext httpContext)
            => ownPort ?? httpContext.Connection.LocalPort;

        return HttpContextExtractors.Subdomain(".localhost")
            .Or(HttpContextExtractors.PortOffset(tenant0Port, tenantCount, PortExtractor).WithPrefix("tenant"))
            .WithValidator(value => {
                if (!value.StartsWith("tenant", StringComparison.Ordinal))
                    throw new ArgumentOutOfRangeException(nameof(value), $"Invalid Tenant ID: '{value}'.");
            });
    }
}
