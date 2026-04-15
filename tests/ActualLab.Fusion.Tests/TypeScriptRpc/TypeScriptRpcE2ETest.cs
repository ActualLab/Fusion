using ActualLab.Rpc;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptRpcE2ETest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    private const string Script = "e2e/ts-dotnet-e2e.ts";

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);

        if (isClient)
            return;

        var rpc = services.AddRpc();
        rpc.AddServer<ITypeScriptTestService, TypeScriptTestService>();
        rpc.AddServer<IServerControlService, ServerControlService>();

        var fusion = services.AddFusion();
        fusion.AddService<ITypeScriptTestComputeService, TypeScriptTestComputeService>(RpcServiceMode.Server);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
    public async Task BasicTypes(string format)
    {
        SerializationFormat = format;
        await using var _ = await WebHost.Serve();
        await RunScenario("basic-types", format);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
    public async Task OverloadResolution(string format)
    {
        SerializationFormat = format;
        await using var _ = await WebHost.Serve();
        await RunScenario("overload-resolution", format);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
    public async Task ComputeInvalidation(string format)
    {
        SerializationFormat = format;
        await using var _ = await WebHost.Serve();
        await RunScenario("compute-invalidation", format);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
    public async Task StreamNoReconnect(string format)
    {
        SerializationFormat = format;
        await using var _ = await WebHost.Serve();
        await RunScenario("stream-no-reconnect", format);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
    public async Task AutoReconnection(string format)
    {
        SerializationFormat = format;
        await using var _ = await WebHost.Serve();
        await RunScenario("auto-reconnect", format);
    }

    [Fact]
    public async Task ReconnectionTorture()
    {
        SerializationFormat = "json5";
        await using var _ = await WebHost.Serve();
        await RunScenario("reconnection-torture", "json5", TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task ServerRestart()
    {
        SerializationFormat = "json5";
        ServerControlService.Reset();

        var serving = await WebHost.Serve();
        var tsTask = RunScenario("server-restart", "json5", TimeSpan.FromSeconds(30));

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

    private Task RunScenario(string scenario, string format, TimeSpan? timeout = null)
    {
        var serverUrl = $"ws://127.0.0.1:{WebHost.ServerUri.Port}/rpc/ws";
        var ts = new TypeScriptRunner(Out);
        return ts.RunScenario(Script, scenario,
            new Dictionary<string, string> {
                ["RPC_SERVER_URL"] = serverUrl,
                ["RPC_FORMAT"] = format,
            },
            timeout);
    }
}
