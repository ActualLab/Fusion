namespace ActualLab.Fusion.Client.Internal;

internal static class AlwaysSynchronized
{
    public static readonly AsyncTaskMethodBuilder Source
        = AsyncTaskMethodBuilderExt.New().WithResult();
}
