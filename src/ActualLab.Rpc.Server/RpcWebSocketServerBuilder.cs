namespace ActualLab.Rpc.Server;

/// <summary>
/// Builder for configuring the <see cref="RpcWebSocketServer"/> and its options.
/// </summary>
public readonly struct RpcWebSocketServerBuilder
{
    public RpcBuilder Rpc { get; }
    public IServiceCollection Services => Rpc.Services;

    internal RpcWebSocketServerBuilder(
        RpcBuilder rpc,
        Action<RpcWebSocketServerBuilder>? configure)
    {
        Rpc = rpc;
        var services = Services;
        if (services.HasService<RpcWebSocketServer>()) {
            configure?.Invoke(this);
            return;
        }

        services.AddSingleton(_ => RpcWebSocketServerDefaultDelegates.PeerRefFactory);
        services.AddSingleton(_ => RpcWebSocketServerOptions.Default);
        services.AddSingleton(c => new RpcWebSocketServer(c.GetRequiredService<RpcWebSocketServerOptions>(), c));
        configure?.Invoke(this);
    }

    public RpcWebSocketServerBuilder Configure(Func<IServiceProvider, RpcWebSocketServerOptions> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }
}
