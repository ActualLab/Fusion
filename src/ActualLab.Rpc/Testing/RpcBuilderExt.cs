namespace ActualLab.Rpc.Testing;

public static class RpcBuilderExt
{
    extension(RpcBuilder rpc)
    {
        public RpcBuilder AddTestClient(Func<IServiceProvider, RpcTestClient.Options>? optionsFactory = null)
        {
            var services = rpc.Services;
            services.AddSingleton(optionsFactory, _ => RpcTestClient.Options.Default);
            if (services.HasService<RpcTestClient>())
                return rpc;

            services.AddSingleton(c => new RpcTestClient(
                c.GetRequiredService<RpcTestClient.Options>(), c));
            services.AddAlias<RpcClient, RpcTestClient>();
            services.AddSingleton(c => new RpcClientPeerReconnectDelayer(c) {
                Delays = RetryDelaySeq.Fixed(0.05),
            });
            services.AddSingleton<RpcPeerOptions>(_ => RpcTestClientPeerOptions.Default);
            return rpc;
        }
    }
}
