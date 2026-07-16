using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
public abstract class FusionBenchmarkBase
{
    private ServiceProvider? _services;

    protected IBenchmarkComputeService Service { get; private set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<IBenchmarkComputeService, BenchmarkComputeService>();
        _services = services.BuildServiceProvider();
        Service = _services.GetRequiredService<IBenchmarkComputeService>();
        OnSetup();
    }

    [GlobalCleanup]
    public void Cleanup()
        => _services?.Dispose();

    protected virtual void OnSetup() { }
}
