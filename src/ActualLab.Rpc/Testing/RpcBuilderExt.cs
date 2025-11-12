namespace ActualLab.Rpc.Testing;

public static class RpcBuilderExt
{
    extension(RpcBuilder rpc)
    {
        public RpcBuilder AddTestClient(Func<IServiceProvider, RpcTestClientOptions>? optionsFactory = null)
        {
            var services = rpc.Services;
            services.AddSingleton(optionsFactory, _ => RpcTestClientOptions.Default);
            if (services.HasService<RpcTestClient>())
                return rpc;

            services.AddSingleton(c => new RpcTestClient(c));
            services.AddAlias<RpcClient, RpcTestClient>();
            services.AddSingleton(c => new RpcClientPeerReconnectDelayer(c) {
                Delays = RetryDelaySeq.Fixed(0.05),
            });
            services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
                ServerPeerShutdownTimeoutProvider = static _ => TimeSpan.FromSeconds(10),
            });
            return rpc;
        }
    }
}
