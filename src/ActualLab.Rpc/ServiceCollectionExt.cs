namespace ActualLab.Rpc;

public static class ServiceCollectionExt
{
    extension(IServiceCollection services)
    {
        public RpcBuilder AddRpc(
            RpcServiceMode defaultServiceMode = RpcServiceMode.Default,
            bool setDefaultServiceMode = false)
            => new(services, null, defaultServiceMode, setDefaultServiceMode);

        public IServiceCollection AddRpc(Action<RpcBuilder> configure)
            => new RpcBuilder(services, configure, RpcServiceMode.Default, false).Services;

        public IServiceCollection AddRpc(
            RpcServiceMode defaultServiceMode,
            Action<RpcBuilder> configure)
            => new RpcBuilder(services, configure, defaultServiceMode, false).Services;

        public IServiceCollection AddRpc(
            RpcServiceMode defaultServiceMode,
            bool setDefaultServiceMode,
            Action<RpcBuilder> configure)
            => new RpcBuilder(services, configure, defaultServiceMode, setDefaultServiceMode).Services;
    }
}
