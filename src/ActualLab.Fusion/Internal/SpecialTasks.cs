namespace ActualLab.Fusion.Internal;

public static class SpecialTasks
{
    public static readonly Task MustReturn = new TaskCompletionSource<Unit>()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
    public static readonly Task MustThrow = new TaskCompletionSource<Unit>()
        .WithException(ActualLab.Internal.Errors.InternalError("This task should never be awaited."))
        .Task;
}
