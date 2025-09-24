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

    public static Task<T[]> WhenAll<T>(Task<T>[] tasks, ITestOutputHelper @out)
        => WhenAll(tasks, TimeSpan.FromSeconds(10), @out);
    public static async Task<T[]> WhenAll<T>(Task<T>[] tasks, TimeSpan timeout, ITestOutputHelper @out)
    {
        var whenAll = Task.WhenAll(tasks).WaitAsync(timeout);
        for (var i = 0;; i++) {
            await Task.WhenAny(whenAll, Task.Delay(i == 0 ? 3000 : 1000));
            if (whenAll.IsCompleted)
                break;

            var remaining = tasks
                .Select((task, index) => (task, index))
                .Where(p => !p.task.IsCompleted)
                .ToList();
            if (remaining.Count == 0)
                break;

            @out.WriteLine($"Waiting for: {remaining.Select(p => $"#{p.index}").ToDelimitedString()}");
        }
        return await whenAll;
    }

    public static IServiceProvider CreateLoggingServices(ITestOutputHelper @out, bool useDebugLog = true)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            if (useDebugLog)
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
            }, TimeSpan.FromSeconds(5));
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
            }, TimeSpan.FromSeconds(3));
        }
        catch (XunitException) {
            @out?.WriteLine($"Shared objects: {peer.SharedObjects.ToDelimitedString()}");
            @out?.WriteLine($"Remote objects: {peer.RemoteObjects.ToDelimitedString()}");
            throw;
        }
    }
}
