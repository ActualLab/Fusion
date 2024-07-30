namespace ActualLab.Fusion.Client.Interception;

internal static class AlwaysSynchronized
{
    public static readonly AsyncTaskMethodBuilder Source
        = AsyncTaskMethodBuilderExt.New().WithResult();
}
