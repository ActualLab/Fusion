namespace ActualLab.Rpc.Server;

/// <summary>
/// Builder for configuring the <see cref="RpcHttpServer"/> and its options.
/// </summary>
public readonly struct RpcHttpServerBuilder
{
    public RpcBuilder Rpc { get; }
    public IServiceCollection Services => Rpc.Services;

    internal RpcHttpServerBuilder(
        RpcBuilder rpc,
        Action<RpcHttpServerBuilder>? configure)
    {
        Rpc = rpc;
        var services = Services;
        if (services.HasService<RpcHttpServer>()) {
            configure?.Invoke(this);
            return;
        }

        services.AddSingleton(_ => RpcHttpServerDefaultDelegates.PeerRefFactory);
        services.AddSingleton(_ => RpcHttpServerOptions.Default);
        services.AddSingleton(c => new RpcHttpServer(c.GetRequiredService<RpcHttpServerOptions>(), c));
        configure?.Invoke(this);
    }

    public RpcHttpServerBuilder Configure(Func<IServiceProvider, RpcHttpServerOptions> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }
}
