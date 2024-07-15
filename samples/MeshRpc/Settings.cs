using ActualLab.Diagnostics;
using ActualLab.Time;

namespace Samples.MeshRpc;

public static class HostFactorySettings
{
    public static readonly Sampler UseHybridMode = Sampler.Never;
    public static readonly double MaxHostCount = 3;
    public static readonly double MinHostCount = 1;
    public static readonly RandomTimeSpan HostTryAddPeriod = TimeSpan.FromSeconds(1).ToRandom(0.5);
    public static readonly RandomTimeSpan HostLifespan = TimeSpan.FromSeconds(5).ToRandom(0.75);
    public static readonly RandomTimeSpan CounterGetDelay = TimeSpan.FromSeconds(1).ToRandom(0.5);
    public static readonly RandomTimeSpan CounterIncrementDelay = TimeSpan.FromSeconds(1).ToRandom(0.5);
}

public static class TestSettings
{
    public static readonly bool MustRunOnClientHost = true;
    public static readonly bool MustRunOnServerHost = true;
    public static readonly int ProcessesPerHost = 3;
    public static readonly TimeSpan TestStopDelay = TimeSpan.FromSeconds(5);
    public static readonly RandomTimeSpan CallPeriod = TimeSpan.FromSeconds(0.25).ToRandom(0.5);
    public static readonly double FusionServiceUseProbability = 1;
    public static readonly double IncrementProbability = 0.25;
}
