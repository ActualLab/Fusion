using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    RecomputeStateOnParameterChange = 0x1,
    AwaitForRecomputeOnParameterChange = 0x2,
    RenderInconsistentState = 0x10,
    ComputeStateOnThreadPool = 0x100,
}

public static class ComputedStateComponentOptionsExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanComputeStateOnThreadPool(this ComputedStateComponentOptions options)
        => HardwareInfo.IsSingleThreaded || (options & ComputedStateComponentOptions.ComputeStateOnThreadPool) != 0;
}
