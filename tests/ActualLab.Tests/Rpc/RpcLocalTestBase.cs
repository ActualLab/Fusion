using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests.Rpc;

public abstract class RpcLocalTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected string SerializationFormat { get; set; } = RpcTestBase.DefaultSerializationFormat;
    protected bool UseLogging { get; set; } = true;
    protected bool UseDebugLog { get; set; } = true;

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
        if (UseLogging)
            services.AddLogging(logging => {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                if (UseDebugLog)
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

    protected Task ConnectionDisruptor(
        string workerId, RpcTestConnection connection, CancellationToken cancellationToken)
        => ConnectionDisruptor(workerId, connection, null, null, cancellationToken);

    protected async Task ConnectionDisruptor(
        string workerId,
        RpcTestConnection connection,
        Func<Random, int>? connectedTime,
        Func<Random, int>? disconnectedTime,
        CancellationToken cancellationToken)
    {
        void Write(string message)
            => Out.WriteLine($"ConnectionDisruptor #{workerId}: {message}");

        Write("started");
        connectedTime ??= rnd => rnd.Next(50, 150);
        disconnectedTime ??= rnd => rnd.Next(10, 40);
        try {
            var rnd = new Random();
            while (true) {
                await Task.Delay(connectedTime.Invoke(rnd), cancellationToken).ConfigureAwait(false);
                // Write("disconnecting");
                await connection.Disconnect(cancellationToken).ConfigureAwait(false);

                await Task.Delay(disconnectedTime.Invoke(rnd), cancellationToken).ConfigureAwait(false);
                // Write("connecting");
                await connection.Connect(cancellationToken).ConfigureAwait(false);
            }
        }
        catch {
            // Intended
        }
        Write("stopping");
        await connection.Connect(CancellationToken.None).ConfigureAwait(false);
        await Delay(0.2).ConfigureAwait(false); // Just in case
        Write("stopped");
    }
}
