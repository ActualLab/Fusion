namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    SynchronizeComputeState = 0x1,
    RecomputeOnParametersSet = 0x2,
}
