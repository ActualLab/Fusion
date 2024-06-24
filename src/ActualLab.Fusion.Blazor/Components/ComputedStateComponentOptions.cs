namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    SynchronizeComputeState = 0x1,
    StateIsParameterDependent = 0x2,
    StateIsPureRenderState = 0x4,
}
