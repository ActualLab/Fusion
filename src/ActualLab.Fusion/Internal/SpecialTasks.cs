namespace ActualLab.Fusion.Internal;

public static class SpecialTasks
{
    public static readonly Task MustReturn = AsyncTaskMethodBuilderExt.New()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
    public static readonly Task MustThrow = AsyncTaskMethodBuilderExt.New()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
}
