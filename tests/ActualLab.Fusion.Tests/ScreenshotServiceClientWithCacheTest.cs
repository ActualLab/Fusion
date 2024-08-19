using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Tests.Services;
using ActualLab.OS;

namespace ActualLab.Fusion.Tests;

public class ScreenshotServiceClientWithCacheTest : FusionTestBase
{
    public ScreenshotServiceClientWithCacheTest(ITestOutputHelper @out) : base(@out)
        => UseRemoteComputedCache = true;

    [Fact]
    public async Task GetScreenshotTest()
    {
        if (OSInfo.IsAnyUnix)
            // Screenshots don't work on Unix
            return;

        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var service1 = ClientServices.GetRequiredService<IScreenshotService>();
        var service2 = clientServices2.GetRequiredService<IScreenshotService>();
        var timeout = TimeSpan.FromSeconds(1);

        var sw = Stopwatch.StartNew();
        var c1 = await GetScreenshotComputed(service1);
        Out.WriteLine($"Miss in: {sw.ElapsedMilliseconds}ms");
        c1.WhenSynchronized().IsCompleted.Should().BeTrue();
        c1.Options.RemoteComputedCacheMode.Should().Be(RemoteComputedCacheMode.Cache);

        sw.Restart();
        var c2 = await GetScreenshotComputed(service2);
        Out.WriteLine($"Hit in: {sw.ElapsedMilliseconds}ms");
        var whenSynchronized = c2.WhenSynchronized();
        whenSynchronized.IsCompleted.Should().BeFalse(); // Service2.GetScreenshotComputed is pulled from cache
        await whenSynchronized;
        c2 = await GetScreenshotComputed(service2);
        c2.WhenSynchronized().IsCompleted.Should().BeTrue();

        sw.Restart();
        await c2.WhenInvalidated().WaitAsync(timeout);
        Out.WriteLine($"Invalidated in: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        c2 = await GetScreenshotComputed(service2);
        Out.WriteLine($"Updated in: {sw.ElapsedMilliseconds}ms");
        c2.WhenSynchronized().IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetScreenshotAltTest()
    {
        if (OSInfo.IsAnyUnix)
            // Screenshots don't work on Unix
            return;

        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var service1 = ClientServices.GetRequiredService<IScreenshotService>();
        var service2 = clientServices2.GetRequiredService<IScreenshotService>();
        var timeout = TimeSpan.FromSeconds(1);

        var sw = Stopwatch.StartNew();
        var c1 = await GetScreenshotAltComputed(service1);
        Out.WriteLine($"Miss in: {sw.ElapsedMilliseconds}ms");
        c1.Output.Value.Should().NotBeNull();
        c1.Options.RemoteComputedCacheMode.Should().Be(RemoteComputedCacheMode.NoCache);
        c1.WhenSynchronized().IsCompleted.Should().BeTrue();

        sw.Restart();
        await c1.WhenInvalidated().WaitAsync(timeout);
        Out.WriteLine($"Invalidated in: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        var c2 = await GetScreenshotAltComputed(service2);
        Out.WriteLine($"2nd miss in: {sw.ElapsedMilliseconds}ms");
        c2.Output.Value.Should().NotBeNull();
        c2.WhenSynchronized().IsCompleted.Should().BeTrue();

        sw.Restart();
        await c2.WhenInvalidated().WaitAsync(timeout);
        Out.WriteLine($"Invalidated in: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        c2 = (RemoteComputed<Screenshot>)await c2.Update().ConfigureAwait(false);
        Out.WriteLine($"Updated in: {sw.ElapsedMilliseconds}ms");
        c2.Output.Value.Should().NotBeNull();
        c2.WhenSynchronized().IsCompleted.Should().BeTrue();
    }

    private static async Task<RemoteComputed<Screenshot>> GetScreenshotComputed(IScreenshotService service)
        => (RemoteComputed<Screenshot>)await Computed.Capture(() => service.GetScreenshot(100));

    private static async Task<RemoteComputed<Screenshot>> GetScreenshotAltComputed(IScreenshotService service)
        => (RemoteComputed<Screenshot>)await Computed.Capture(() => service.GetScreenshotAlt(100));
}
