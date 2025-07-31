using Owin;

namespace ActualLab.Rpc.Server;

public static class EndpointRouteBuilderExt
{
    public static IAppBuilder MapRpcServer(this IAppBuilder appBuilder, IServiceProvider services)
    {
        if (appBuilder is null) throw new ArgumentNullException(nameof(appBuilder));
        if (services is null) throw new ArgumentNullException(nameof(services));

        var server = services.GetRequiredService<RpcWebSocketServer>();
        var settings = server.Settings;

        appBuilder = appBuilder.Map(server.Settings.RequestPath, app => MapEndpoint(app, false));
        if (settings.ExposeBackend)
            appBuilder = appBuilder.Map(server.Settings.BackendRequestPath, app => MapEndpoint(app, true));
        return appBuilder;

        void MapEndpoint(IAppBuilder app, bool isBackend)
            => app.Run(context => {
                var statusCode = server.Invoke(context, isBackend);
                context.Response.StatusCode = (int)statusCode;
                return Task.CompletedTask;
            });
    }
}
