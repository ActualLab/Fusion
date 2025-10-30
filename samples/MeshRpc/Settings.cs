using ActualLab.Diagnostics;

namespace Samples.MeshRpc;

public static class MeshSettings
{
    public const int ShardCount = 12;
}

public static class HostFactorySettings
{
    public static readonly Sampler UseRemoteComputedCacheSampler = Sampler.RandomShared(0.5);
    public static readonly double MinHostCount = 0;
    public static readonly double MaxHostCount = 8;
    public static readonly RandomTimeSpan HostTryAddPeriod = TimeSpan.FromSeconds(1).ToRandom(0.5);
    public static readonly RandomTimeSpan HostLifespan = TimeSpan.FromSeconds(5).ToRandom(0.75);
    public static readonly RandomTimeSpan CounterGetDelay = TimeSpan.FromSeconds(0.75).ToRandom(0.5);
    public static readonly RandomTimeSpan CounterIncrementDelay = TimeSpan.FromSeconds(0.75).ToRandom(0.5);
}

public static class TestSettings
{
    public static readonly int CounterCount = 20;
    public static readonly int MaxRetryCount = 5;
    public static readonly Sampler UseFusionSampler = Sampler.RandomShared(0.75);
    public static readonly Sampler IncrementSampler = Sampler.RandomShared(0.25);
    public static readonly bool MustRunOnClientHost = true;
    public static readonly bool MustRunOnServerHost = true;
    public static readonly int ProcessesPerHost = 5;
    public static readonly RandomTimeSpan CallPeriod = TimeSpan.FromSeconds(0.25).ToRandom(0.5);
    public static readonly TimeSpan TestStopDelay = TimeSpan.FromSeconds(5);
}
