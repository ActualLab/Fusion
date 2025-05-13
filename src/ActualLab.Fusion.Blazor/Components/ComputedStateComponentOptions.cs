using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    RecomputeStateOnParameterChange = 0x1,
    RenderInconsistentState = 0x10,
    RenderOnceInitializedAsync = 0x100,
    RenderOnceParametersSet = 0x200,
    RenderOnceParametersSetAsync = 0x400,
    ComputeStateOnThreadPool = 0x1000,
}

public static class ComputedStateComponentOptionsExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanComputeStateOnThreadPool(this ComputedStateComponentOptions options)
        => HardwareInfo.IsSingleThreaded || (options & ComputedStateComponentOptions.ComputeStateOnThreadPool) != 0;
}
