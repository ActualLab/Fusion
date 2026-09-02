#if NETCOREAPP
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ActualLab.Tests.Rpc;

[Trait("Category", "Rpc")]
public class RpcServerStoppingGateTest
{
    [Fact]
    public async Task WebSocketServerShouldRejectWhileStopping()
    {
        // arrange
        var lifetime = new FakeHostLifetime();
        await using var services = NewServices(lifetime, new RpcWebSocketServerOptions());
        var server = services.GetRequiredService<RpcWebSocketServer>();
        lifetime.StopApplication();

        // act
        var context = new DefaultHttpContext { RequestServices = services };
        await server.Invoke(context, isBackend: false);

        // assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task WebSocketServerShouldNotRejectBeforeStopping()
    {
        // arrange
        var lifetime = new FakeHostLifetime();
        await using var services = NewServices(lifetime, new RpcWebSocketServerOptions());
        var server = services.GetRequiredService<RpcWebSocketServer>();

        // act
        var context = new DefaultHttpContext { RequestServices = services };
        await server.Invoke(context, isBackend: false);

        // assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "a non-WebSocket request must reach the regular checks");
    }

    [Fact]
    public async Task WebSocketServerShouldNotRejectWhenTheGateIsOff()
    {
        // arrange
        var lifetime = new FakeHostLifetime();
        await using var services = NewServices(lifetime, new RpcWebSocketServerOptions {
            MustRejectOnApplicationStopping = false,
        });
        var server = services.GetRequiredService<RpcWebSocketServer>();
        lifetime.StopApplication();

        // act
        var context = new DefaultHttpContext { RequestServices = services };
        await server.Invoke(context, isBackend: false);

        // assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

#if NET5_0_OR_GREATER
    [Fact]
    public async Task HttpServerShouldRejectWhileStopping()
    {
        // arrange
        var lifetime = new FakeHostLifetime();
        await using var services = NewServices(lifetime, new RpcHttpServerOptions());
        var server = services.GetRequiredService<RpcHttpServer>();
        lifetime.StopApplication();

        // act
        var context = new DefaultHttpContext { RequestServices = services };
        await server.Invoke(context, isBackend: false);

        // assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task HttpServerShouldNotRejectBeforeStopping()
    {
        // arrange
        var lifetime = new FakeHostLifetime();
        await using var services = NewServices(lifetime, new RpcHttpServerOptions());
        var server = services.GetRequiredService<RpcHttpServer>();

        // act
        var context = new DefaultHttpContext { RequestServices = services };
        await server.Invoke(context, isBackend: false);

        // assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status426UpgradeRequired,
            "an HTTP/1.1 request must reach the regular checks");
    }
#endif

    // Private methods

    private static ServiceProvider NewServices(IHostApplicationLifetime lifetime, RpcWebSocketServerOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(lifetime);
        services.AddRpc().AddWebSocketServer();
        services.AddSingleton(_ => options);
        return services.BuildServiceProvider();
    }

#if NET5_0_OR_GREATER
    private static ServiceProvider NewServices(IHostApplicationLifetime lifetime, RpcHttpServerOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(lifetime);
        services.AddRpc().AddHttpServer();
        services.AddSingleton(_ => options);
        return services.BuildServiceProvider();
    }
#endif

    // Nested types

    private sealed class FakeHostLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stoppingCts = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stoppingCts.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
            => _stoppingCts.Cancel();
    }
}
#endif
