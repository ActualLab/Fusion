#if NETCOREAPP
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using ActualLab.Testing.Logging;
using Microsoft.AspNetCore.Builder;

namespace ActualLab.Tests.Rpc;

[Trait("Category", "Rpc")]
public class RpcWebSocketServerOriginWarningTest
{
    [Fact]
    public async Task WarnsWhenNothingValidatesTheOrigin()
    {
        var log = await Resolve(new RpcWebSocketServerOptions {
            OriginValidator = RpcWebSocketServerOriginValidators.AllowAll,
        });

        log.Should().Contain("OriginValidator");
    }

    [Fact]
    public async Task DoesNotWarnWhenOriginIsValidated()
    {
        var log = await Resolve(new RpcWebSocketServerOptions {
            OriginValidator = RpcWebSocketServerOriginValidators.SameOrigin,
        });

        log.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotWarnWhenTheWarningIsTurnedOff()
    {
        var log = await Resolve(new RpcWebSocketServerOptions {
            OriginValidator = RpcWebSocketServerOriginValidators.AllowAll,
            WarnOnUnvalidatedOrigin = false,
        });

        log.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotWarnWhenWebSocketOptionsCarryAllowedOrigins()
    {
        var log = await Resolve(
            new RpcWebSocketServerOptions { OriginValidator = RpcWebSocketServerOriginValidators.AllowAll },
            services => services.Configure<WebSocketOptions>(
                o => o.AllowedOrigins.Add("https://example.com")));

        log.Should().BeEmpty();
    }

    // Private methods

    private static async Task<string> Resolve(
        RpcWebSocketServerOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders().AddProvider(loggerProvider));
        services.AddRpc().AddWebSocketServer();
        services.AddSingleton(_ => options);
        configureServices?.Invoke(services);
        await using var serviceProvider = services.BuildServiceProvider();

        // The warning is emitted by the constructor, and the server is a singleton -
        // so resolving it once is the whole trigger.
        _ = serviceProvider.GetRequiredService<RpcWebSocketServer>();
        return loggerProvider.Content;
    }
}
#endif
