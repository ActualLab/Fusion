using ActualLab.Testing.Logging;

namespace ActualLab.Tests.Rpc;

public class RpcWebHostTest
{
    [Fact]
    public void UsesOnlyBaseServiceLoggingProviders()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging
            .ClearProviders()
            .AddProvider(loggerProvider));

        using var host = new RpcWebHost(services);

        host.Services.GetServices<ILoggerProvider>()
            .Should()
            .Equal(loggerProvider);
    }
}
