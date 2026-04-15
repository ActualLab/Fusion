using ActualLab.Rpc;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptRpcBinaryE2ETest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    private const string Script = "e2e/ts-dotnet-e2e.ts";

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        SerializationFormat = "msgpack6";
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
    public async Task StreamNoReconnect()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("stream-no-reconnect");
    }

    [Fact]
    public async Task AutoReconnection()
    {
        await using var _ = await WebHost.Serve();
        await RunScenario("auto-reconnect");
    }

    private Task RunScenario(string scenario, TimeSpan? timeout = null)
    {
        var serverUrl = $"ws://127.0.0.1:{WebHost.ServerUri.Port}/rpc/ws";
        var ts = new TypeScriptRunner(Out);
        return ts.RunScenario(Script, scenario,
            new Dictionary<string, string> {
                ["RPC_SERVER_URL"] = serverUrl,
                ["RPC_FORMAT"] = "msgpack6",
            },
            timeout);
    }
}
