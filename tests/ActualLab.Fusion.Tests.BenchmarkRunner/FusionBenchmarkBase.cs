using ActualLab.Rpc;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
public abstract class FusionBenchmarkBase
{
    private ServiceProvider? _services;

    protected IBenchmarkComputeService Service { get; private set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<IBenchmarkComputeService, BenchmarkComputeService>();
        services.AddSingleton(_ => RpcDiagnosticsOptions.Default with {
            MustPropagateAmbientActivityContext = false,
        });
        _services = services.BuildServiceProvider();
        Service = _services.GetRequiredService<IBenchmarkComputeService>();
        OnSetup();
    }

    [GlobalCleanup]
    public void Cleanup()
        => _services?.Dispose();

    protected virtual void OnSetup() { }
}
