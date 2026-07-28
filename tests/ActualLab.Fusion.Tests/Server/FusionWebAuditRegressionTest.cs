#if !NETFRAMEWORK
using ActualLab.Fusion.Server.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ActualLab.Fusion.Tests.Server;

public class FusionWebAuditRegressionTest
{
    [Fact]
    public async Task InvalidSessionHandlerShouldBeAbleToShortCircuitThePipeline()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddScoped<ISessionValidator, RejectingSessionValidator>();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var nextCallCount = 0;
        var options = new SessionMiddleware.Options {
            InvalidSessionHandler = (_, _) => Task.FromResult(true),
        };
        var middleware = new SessionMiddleware(options, scope.ServiceProvider);
        var context = NewHttpContext(scope.ServiceProvider, "FusionAuth.SessionId=session-id");

        await middleware.InvokeAsync(context, _ => {
            nextCallCount++;
            return Task.CompletedTask;
        });

        nextCallCount.Should().Be(0);
        context.Response.Headers.SetCookie.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvalidSessionShouldBeReplacedBeforeTheRedirect()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddScoped<ISessionValidator>(_ => new SessionIdRejector("invalid-session-id"));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var middleware = new SessionMiddleware(new SessionMiddleware.Options(), scope.ServiceProvider);
        var nextCallCount = 0;

        var context1 = NewHttpContext(scope.ServiceProvider, "FusionAuth.SessionId=invalid-session-id");
        await middleware.InvokeAsync(context1, _ => {
            nextCallCount++;
            return Task.CompletedTask;
        });

        context1.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        nextCallCount.Should().Be(0);

        var context2 = NewHttpContext(scope.ServiceProvider, ReplayCookies(context1));
        await middleware.InvokeAsync(context2, _ => {
            nextCallCount++;
            return Task.CompletedTask;
        });

        context2.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PersistentlyInvalidSessionShouldNotRedirectTwice()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddScoped<ISessionValidator, RejectingSessionValidator>();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var middleware = new SessionMiddleware(new SessionMiddleware.Options(), scope.ServiceProvider);
        var nextCallCount = 0;

        var context1 = NewHttpContext(scope.ServiceProvider, "FusionAuth.SessionId=invalid-session-id");
        await middleware.InvokeAsync(context1, _ => {
            nextCallCount++;
            return Task.CompletedTask;
        });

        context1.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        nextCallCount.Should().Be(0);

        var context2 = NewHttpContext(scope.ServiceProvider, ReplayCookies(context1));
        await middleware.InvokeAsync(context2, _ => {
            nextCallCount++;
            return Task.CompletedTask;
        });

        context2.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task MalformedSessionCookieShouldBeReplaced()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var invalidSessionCallCount = 0;
        var options = new SessionMiddleware.Options {
            InvalidSessionHandler = (_, _) => {
                invalidSessionCallCount++;
                return TaskExt.FalseTask;
            },
        };
        var middleware = new SessionMiddleware(options, scope.ServiceProvider);
        var context = NewHttpContext(scope.ServiceProvider, "FusionAuth.SessionId=x");

        var action = () => middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await action.Should().NotThrowAsync();
        invalidSessionCallCount.Should().Be(1);
        scope.ServiceProvider.GetRequiredService<ISessionResolver>().Session.Id.Should().NotBe("x");
        context.Response.Headers.SetCookie.Should().NotBeEmpty();
    }

    [Fact]
    public void SubdomainExtractorShouldRequireTheConfiguredSuffixAtTheEnd()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("tenant.example.com.attacker.test");
        var extractor = HttpContextExtractors.Subdomain(".example.com");

        extractor(context).Should().BeEmpty();
        context.Request.Host = new HostString("tenant.example.com");
        extractor(context).Should().Be("tenant");
        HttpContextExtractors.Subdomain()(context).Should().Be("tenant");
    }

    private static DefaultHttpContext NewHttpContext(IServiceProvider services, string cookie)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Cookie = cookie;
        return context;
    }

    private static string ReplayCookies(HttpContext httpContext)
    {
        var setCookieHeaders = httpContext.Response.Headers.SetCookie.Select(x => x ?? "").ToArray();
        var setCookies = SetCookieHeaderValue.ParseList(setCookieHeaders);
        return setCookies
            .Where(x => x.Expires is not { } expires || expires > DateTimeOffset.UtcNow)
            .Select(x => $"{x.Name}={x.Value}")
            .ToDelimitedString("; ");
    }

    private sealed class RejectingSessionValidator : ISessionValidator
    {
        public Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
            => TaskExt.FalseTask;
    }

    private sealed class SessionIdRejector(string sessionId) : ISessionValidator
    {
        public Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
            => string.Equals(session.Id, sessionId, StringComparison.Ordinal)
                ? TaskExt.FalseTask
                : TaskExt.TrueTask;
    }
}
#endif
