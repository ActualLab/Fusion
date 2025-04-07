using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests.Rpc;

public abstract class RpcLocalTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    public string SerializationFormat { get; set; } = RpcSerializationFormatResolver.Default.DefaultClientFormatKey;

    protected virtual ServiceProvider CreateServices(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        configureServices?.Invoke(services);

        var c = services.BuildServiceProvider();
        StartServices(c);
        return c;
    }

    protected virtual void StartServices(IServiceProvider services)
    {
        var testClient = services.GetRequiredService<RpcTestClient>();
        var connection = testClient.CreateDefaultConnection();
        _ = connection.Connect();
    }

    protected virtual void ConfigureServices(ServiceCollection services)
    {
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
            logging.AddProvider(
#pragma warning disable CS0618
                new XunitTestOutputLoggerProvider(
                    new TestOutputHelperAccessor() { Output = Out },
                    (_, level) => level >= LogLevel.Debug));
#pragma warning restore CS0618
        });

        var rpc = services.AddRpc();
        rpc.AddTestClient();
        services.AddSingleton<RpcPeerFactory>(_ =>
            (hub, peerRef) => peerRef.IsServer
                ? new RpcServerPeer(hub, peerRef)
                : new RpcClientPeer(hub, peerRef)
        );
        services.AddSingleton<RpcSerializationFormatResolver>(
            _ => new RpcSerializationFormatResolver(SerializationFormat, RpcSerializationFormat.All.ToArray()));
    }
}
