using ActualLab.Rpc;
using ActualLab.Testing.Collections;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TypeScriptRpcPerfTest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    private const string Script = "e2e/ts-dotnet-perf.ts";

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        SerializationFormat = "json5np";
        RpcFrameDelayerFactory = null;
        base.ConfigureServices(services, isClient);

        if (isClient)
            return;

        var fusion = services.AddFusion();
        fusion.AddService<ITypeScriptTestComputeService, TypeScriptTestComputeService>(RpcServiceMode.Server);
    }

    [Theory]
    [InlineData("compute", 32, 100_000)]
    public async Task ComputePerformance(string scenario, int workerCount, int iterCount)
    {
        await using var _ = await WebHost.Serve();
        await RunScenario(scenario, workerCount, iterCount);
    }

    [Theory]
    [InlineData("compute-rpc-same", 32, 100_000)]
    public async Task ComputeRpcSamePerformance(string scenario, int workerCount, int iterCount)
    {
        await using var _ = await WebHost.Serve();
        await RunScenario(scenario, workerCount, iterCount);
    }

    [Theory]
    [InlineData("compute-rpc-unique", 32, 5_000)]
    public async Task ComputeRpcUniquePerformance(string scenario, int workerCount, int iterCount)
    {
        await using var _ = await WebHost.Serve();
        await RunScenario(scenario, workerCount, iterCount);
    }

    [Theory]
    [InlineData("rpc", 32, 5_000)]
    public async Task RpcPerformance(string scenario, int workerCount, int iterCount)
    {
        await using var _ = await WebHost.Serve();
        await RunScenario(scenario, workerCount, iterCount);
    }

    [Theory]
    [InlineData("stream", 32, 2_000, 10)]
    [InlineData("stream", 32, 50, 5_000)]
    public async Task StreamPerformance(string scenario, int workerCount, int iterCount, int itemCount)
    {
        await using var _ = await WebHost.Serve();
        await RunScenario(scenario, workerCount, iterCount, itemCount);
    }

    private Task RunScenario(string scenario, int workerCount, int iterCount, int itemCount = 0)
    {
        var serverUrl = $"ws://127.0.0.1:{WebHost.ServerUri.Port}/rpc/ws";
        var ts = new TypeScriptRunner(Out);
        var env = new Dictionary<string, string> {
            ["RPC_SERVER_URL"] = serverUrl,
            ["WORKER_COUNT"] = workerCount.ToString(),
            ["ITER_COUNT"] = iterCount.ToString(),
        };
        if (itemCount > 0)
            env["ITEM_COUNT"] = itemCount.ToString();
        return ts.RunScenario(Script, scenario, env, TimeSpan.FromMinutes(2));
    }
}
