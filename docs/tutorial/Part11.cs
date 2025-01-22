using System.Net;
using ActualLab.Conversion;
using ActualLab.Fusion.Authentication.Services;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using static System.Console;

namespace Tutorial;

public static class Part11{
    public class Startup
    {
            
        // public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        // {
        //     // Note that now it's slightly more complex due to
        //     // newly introduced multitenancy support in Fusion 3.x.
        //     // But you'll get the idea.

        //     var cookies = httpContext.Request.Cookies;
        //     var cookieName = Cookie.Name ?? "";
        //     cookies.TryGetValue(cookieName, out var sessionId);
        //     var session = string.IsNullOrEmpty(sessionId) ? null : new Session(sessionId);

        //     if (session == null) {
        //         session = SessionFactory.CreateSession();
        //         var responseCookies = httpContext.Response.Cookies;
        //         responseCookies.Append(cookieName, session.Id, Cookie.Build(httpContext));
        //     }
        //     SessionProvider.Session = session;
        //     await next(httpContext).ConfigureAwait(false);
        // }

        public void ConfigureServices(IServiceCollection services)
        {
            
        }
    }

    #region Part11_AppDbContext
    public class AppDbContext : DbContextBase
    {
        // Authentication-related tables
        public DbSet<DbUser<long>> Users { get; protected set; } = null!;
        public DbSet<DbUserIdentity<long>> UserIdentities { get; protected set; } = null!;
        public DbSet<DbSessionInfo<long>> Sessions { get; protected set; } = null!;
        // Operations Framework's operation log
        public DbSet<DbOperation> Operations { get; protected set; } = null!;

        public AppDbContext(DbContextOptions options) : base(options) { }
    }
    #endregion
}
