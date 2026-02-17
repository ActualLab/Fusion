using ActualLab.Rpc;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptRpcE2ETest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    private const string Script = "e2e/ts-dotnet-e2e.ts";

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        SerializationFormat = "json5";
        base.ConfigureServices(services, isClient);

        if (isClient)
            return;

        var rpc = services.AddRpc();
        rpc.AddServer<ITypeScriptTestService, TypeScriptTestService>();
        rpc.AddServer<IServerControlService, ServerControlService>();

        var fusion = services.AddFusion();
        fusion.AddService<ITypeScriptTestComputeService, TypeScriptTestComputeService>(RpcServiceMode.Server);
    }

    [Fact]
    public async Task BasicTypes()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("basic-types");
    }

    [Fact]
    public async Task OverloadResolution()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("overload-resolution");
    }

    [Fact]
    public async Task ComputeInvalidation()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("compute-invalidation");
    }

    [Fact]
    public async Task AutoReconnection()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("auto-reconnect");
    }

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

        // Start TS script in background — it will signal RequestRestart when ready
        var tsTask = RunScenario("server-restart", TimeSpan.FromSeconds(30));

        // Wait for the TS script to signal a restart via RequestRestart RPC call
        await ServerControlService.WhenRestartRequested;

        // Stop the server (disposes the host, resets HostLazy for next Serve)
        await serving.DisposeAsync();

        // Brief delay, then restart on the same port
        await Task.Delay(200);
        serving = await WebHost.Serve();

        try {
            // Wait for the TS script to finish (it reconnects and verifies state reset)
            await tsTask;
        }
        finally {
            await serving.DisposeAsync();
        }
    }

    private Task RunScenario(string scenario, TimeSpan? timeout = null)
    {
        var serverUrl = $"ws://127.0.0.1:{WebHost.ServerUri.Port}/rpc/ws";
        var ts = new TypeScriptRunner(Out);
        return ts.RunScenario(Script, scenario,
            new Dictionary<string, string> { ["RPC_SERVER_URL"] = serverUrl },
            timeout);
    }
}
