#if !NETFRAMEWORK
using ActualLab.Fusion.Server.Middlewares;
using Microsoft.AspNetCore.Http;

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
        context.Response.Headers.SetCookie.Should().BeEmpty();
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

    private sealed class RejectingSessionValidator : ISessionValidator
    {
        public Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
            => TaskExt.FalseTask;
    }
}
#endif
