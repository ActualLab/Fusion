#if !NETFRAMEWORK
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Internal;
using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using ActualLab.Testing.Logging;

namespace ActualLab.Fusion.Tests.Server;

public class RpcOriginValidatorWarnerTest
{
    [Fact]
    public async Task WarnsWhenSessionBoundConnectionsHaveNoOriginCheck()
    {
        var log = await Warn(
            new RpcPeerOptions().WithFusionServerOverrides(),
            RpcWebSocketServerOriginValidators.AllowAll);

        log.Should().Contain("OriginValidator");
    }

    [Fact]
    public async Task DoesNotWarnWhenOriginIsValidated()
    {
        var log = await Warn(
            new RpcPeerOptions().WithFusionServerOverrides(),
            RpcWebSocketServerOriginValidators.SameOrigin);

        log.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotWarnWhenConnectionsAreNotSessionBound()
    {
        var log = await Warn(new RpcPeerOptions(), RpcWebSocketServerOriginValidators.AllowAll);

        log.Should().BeEmpty();
    }

    // Private methods

    private static async Task<string> Warn(
        RpcPeerOptions peerOptions,
        RpcWebSocketServerOriginValidator originValidator)
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders().AddProvider(loggerProvider));
        services.AddFusion().AddWebServer();
        services.AddSingleton(_ => peerOptions);
        services.AddSingleton(_ => new RpcWebSocketServerOptions() { OriginValidator = originValidator });
        await using var serviceProvider = services.BuildServiceProvider();

        await new RpcOriginValidatorWarner(serviceProvider).StartAsync(CancellationToken.None);
        return loggerProvider.Content;
    }
}
#endif
