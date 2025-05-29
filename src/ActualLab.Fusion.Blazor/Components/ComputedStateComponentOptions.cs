using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

[Flags]
public enum ComputedStateComponentOptions
{
    RecomputeStateOnParameterChange = 0x1,
    RenderInconsistentState = 0x10,
    // UseInitializedRenderPoint isn't needed, coz it's always enabled
    UseParametersSetRenderPoint = 0x100,
    UseInitializedAsyncRenderPoint = 0x200,
    UseParametersSetAsyncRenderPoint = 0x400,
    // Blazor's ComponentBase uses all these render points
    UseAllRenderPoints = UseParametersSetRenderPoint | UseInitializedAsyncRenderPoint | UseParametersSetAsyncRenderPoint,
    ComputeStateOnThreadPool = 0x1000,
}

public static class ComputedStateComponentOptionsExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanComputeStateOnThreadPool(this ComputedStateComponentOptions options)
        => HardwareInfo.IsSingleThreaded || (options & ComputedStateComponentOptions.ComputeStateOnThreadPool) != 0;
}
