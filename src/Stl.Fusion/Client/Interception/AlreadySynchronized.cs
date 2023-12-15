namespace ActualLab.Fusion.Client.Interception;

internal static class AlwaysSynchronized
{
    public static readonly TaskCompletionSource<Unit> Source
        = TaskCompletionSourceExt.New<Unit>().WithResult(default);
}
