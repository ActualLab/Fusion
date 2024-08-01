using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    ComputeStateOnThreadPool = 0x1,
    RecomputeStateOnParameterChange = 0x2,
    ShouldRenderInconsistentState = 0x4,
}

public static class ComputedStateComponentOptionsExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanComputeStateOnThreadPool(this ComputedStateComponentOptions options)
        => HardwareInfo.IsSingleThreaded || (options & ComputedStateComponentOptions.ComputeStateOnThreadPool) != 0;
}
