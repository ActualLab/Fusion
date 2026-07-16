#if !NETFRAMEWORK
using System.Globalization;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace ActualLab.Fusion.Tests.Server;

public class FusionSessionBoundaryAuditRegressionTest
{
    [Fact]
    public async Task MalformedExplicitSessionBindingShouldFail()
    {
        var result = await BindSession("?session=x");

        result.IsModelSet.Should().BeFalse();
    }

    [Fact]
    public async Task AbsentSessionBindingShouldUseAmbientSession()
    {
        var result = await BindSession("");

        result.Model.Should().Be(new Session("ambient-session"));
    }

    [Fact]
    public async Task DuplicateRpcQuerySessionsShouldUseAmbientSession()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddWebServer();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.QueryString = new QueryString("?session=query-session-1&session=query-session-2");
        context.Request.Headers.Cookie = "FusionAuth.SessionId=ambient-session";
        var properties = PropertyBag.Empty.KeylessSet<HttpContext>(context);
        var factory = new RpcPeerOptions().WithFusionServerOverrides().ServerConnectionFactory;

        var connection = await factory(null!, null!, properties, default);

        connection.Should().BeOfType<SessionBoundRpcConnection>()
            .Which.Session.Should().Be(new Session("ambient-session"));
    }

    private static async Task<ModelBindingResult> BindSession(string queryString)
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISessionResolver>().Session = new Session("ambient-session");
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.QueryString = new QueryString(queryString);
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var valueProvider = new QueryStringValueProvider(
            BindingSource.Query,
            httpContext.Request.Query,
            CultureInfo.InvariantCulture);
        var metadataProvider = new EmptyModelMetadataProvider();
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext,
            valueProvider,
            metadataProvider.GetMetadataForType(typeof(Session)),
            bindingInfo: null,
            modelName: "session");
        var binderType = typeof(FusionWebServerBuilder).Assembly.GetType(
            "ActualLab.Fusion.Server.Internal.SessionModelBinder")!;
        var binder = (IModelBinder)Activator.CreateInstance(binderType)!;

        await binder.BindModelAsync(bindingContext);
        return bindingContext.Result;
    }
}
#endif
