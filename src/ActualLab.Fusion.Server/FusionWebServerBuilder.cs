using ActualLab.Fusion.Server.Authentication;
using ActualLab.Fusion.Server.Endpoints;
using ActualLab.Fusion.Server.Middlewares;
using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;

namespace ActualLab.Fusion.Server;

[StructLayout(LayoutKind.Auto)]
public readonly struct FusionWebServerBuilder
{
    private sealed class AddedTag;
    private static readonly ServiceDescriptor AddedTagDescriptor = new(typeof(AddedTag), new AddedTag());

    public FusionBuilder Fusion { get; }
    public IServiceCollection Services => Fusion.Services;

    internal FusionWebServerBuilder(
        FusionBuilder fusion,
        Action<FusionWebServerBuilder>? configure)
    {
        Fusion = fusion;
        var services = Services;
        if (services.Contains(AddedTagDescriptor)) {
            configure?.Invoke(this);
            return;
        }

        // We want the above Contains call to run in O(1), so...
        services.Insert(0, AddedTagDescriptor);

        // Add Rpc-related services
        var rpc = fusion.Rpc;
        rpc.AddWebSocketServer();
        rpc.AddInboundCallPreprocessor<RpcDefaultSessionReplacer>();

        // Add other services
        services.AddSingleton(_ => SessionMiddleware.Options.Default);
        services.AddScoped(c => new SessionMiddleware(c.GetRequiredService<SessionMiddleware.Options>(), c));
        services.AddSingleton(_ => ServerAuthHelper.Options.Default);
        services.AddScoped(c => new ServerAuthHelper(c.GetRequiredService<ServerAuthHelper.Options>(), c));
        services.AddSingleton(_ => AuthEndpoints.Options.Default);
        services.AddSingleton(c => new AuthEndpoints(c.GetRequiredService<AuthEndpoints.Options>()));
        services.AddSingleton(_ => new RenderModeEndpoint());

        configure?.Invoke(this);
    }

    public FusionMvcWebServerBuilder AddMvc()
        => new(this, null);

    public FusionWebServerBuilder AddMvc(Action<FusionMvcWebServerBuilder> configure)
        => new FusionMvcWebServerBuilder(this, configure).FusionWebServer;

    public FusionWebServerBuilder ConfigureSessionMiddleware(
        Func<IServiceProvider, SessionMiddleware.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    public FusionWebServerBuilder ConfigureAuthEndpoint(
        Func<IServiceProvider, AuthEndpoints.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    public FusionWebServerBuilder ConfigureServerAuthHelper(
        Func<IServiceProvider, ServerAuthHelper.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }
}
