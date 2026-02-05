namespace ActualLab.Fusion.Internal;

/// <summary>
/// Sentinel <see cref="Task"/> instances used as control-flow signals
/// in <see cref="ComputeFunction"/> error handling.
/// </summary>
public static class SpecialTasks
{
    public static readonly Task MustReturn = AsyncTaskMethodBuilderExt.New()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
    public static readonly Task MustThrow = AsyncTaskMethodBuilderExt.New()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
}
