using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptRpcE2ETest(ITestOutputHelper @out)
    : TypeScriptRpcE2ETestBase(@out, "json5")
{
    [Fact]
    public async Task ReconnectionTorture()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("reconnection-torture", TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task ServerRestart()
    {
        ServerControlService.Reset();

        var serving = await WebHost.Serve();

        var tsTask = RunScenario("server-restart", TimeSpan.FromSeconds(30));

        await ServerControlService.WhenRestartRequested;

        await serving.DisposeAsync();

        await Task.Delay(200);
        serving = await WebHost.Serve();

        try {
            await tsTask;
        }
        finally {
            await serving.DisposeAsync();
        }
    }
}
