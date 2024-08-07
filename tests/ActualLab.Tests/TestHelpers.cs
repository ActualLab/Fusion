using ActualLab.Generators;
using ActualLab.Rpc;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;
using Xunit.Sdk;

namespace ActualLab.Tests;

public static class TestHelpers
{
    public static Task Delay(double seconds, CancellationToken cancellationToken = default)
        => Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

    public static Task RandomDelay(double maxSeconds, CancellationToken cancellationToken = default)
    {
        var seconds = RandomShared.NextDouble() * maxSeconds;
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }

    // ReSharper disable once InconsistentNaming
    public static void GCCollect()
    {
        for (var i = 0; i < 3; i++) {
            GC.Collect();
            Thread.Sleep(10);
        }
    }

    public static IServiceProvider CreateLoggingServices(ITestOutputHelper @out)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
            logging.Services.AddSingleton<ILoggerProvider>(_ => {
#pragma warning disable CS0618
                return new XunitTestOutputLoggerProvider(
                    new TestOutputHelperAccessor() { Output = @out },
                    (_, level) => level >= LogLevel.Debug);
#pragma warning restore CS0618
            });
        });
        return services.BuildServiceProvider();
    }

    // Rpc

    public static async Task AssertNoCalls(RpcPeer peer, ITestOutputHelper? @out = null)
    {
        try {
            await TestExt.When(() => {
                peer.InboundCalls.Count.Should().Be(0);
                peer.OutboundCalls.Count.Should().Be(0);
            }, TimeSpan.FromSeconds(1));
        }
        catch (XunitException) {
            @out?.WriteLine($"Inbound calls: {peer.InboundCalls.ToDelimitedString()}");
            @out?.WriteLine($"Outbound calls: {peer.OutboundCalls.ToDelimitedString()}");
            throw;
        }
    }

    public static async Task AssertNoObjects(RpcPeer peer, ITestOutputHelper? @out = null)
    {
        try {
            await TestExt.When(() => {
                peer.SharedObjects.Count.Should().Be(0);
                peer.RemoteObjects.Count.Should().Be(0);
            }, TimeSpan.FromSeconds(1));
        }
        catch (XunitException) {
            @out?.WriteLine($"Shared objects: {peer.SharedObjects.ToDelimitedString()}");
            @out?.WriteLine($"Remote objects: {peer.RemoteObjects.ToDelimitedString()}");
            throw;
        }
    }
}
