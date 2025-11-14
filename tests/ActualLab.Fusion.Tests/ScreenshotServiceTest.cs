using ActualLab.Fusion.Tests.Services;
using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ScreenshotServiceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IScreenshotService, ScreenshotService>();
    }

    [Fact]
    public async Task BasicTest()
    {
        if (OSInfo.IsAnyUnix)
            // Screenshots don't work on Unix
            return;

        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IScreenshotService>();

        var c = await GetScreenshotComputed(client);
        for (var i = 0; i < 10; i++) {
            c.Value.Image.Length.Should().BeGreaterThan(0);
            await TestExt.When(
                () => c.IsConsistent().Should().BeFalse(),
                TimeSpan.FromSeconds(0.5));
            c = await c.Update();
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task ReconnectTest()
    {
        if (OSInfo.IsAnyUnix)
            // Screenshots don't work on Unix
            return;

        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IScreenshotService>();

        var c = await GetScreenshotComputed(client);
        for (var i = 0; i < 50; i++) {
            c.Value.Image.Length.Should().BeGreaterThan(0);
            await TestExt.When(
                () => c.IsConsistent().Should().BeFalse(),
                TimeSpan.FromSeconds(1));
            var updateTask = c.Update();
            if (i % 3 == 0) {
                await RandomDelay(0.2);
                await connection.Reconnect();
            }
            c = await updateTask;
        }
    }

    private ValueTask<Computed<Screenshot>> GetScreenshotComputed(IScreenshotService screenshots)
        => Computed.Capture(() => screenshots.GetScreenshot(1280));
}
