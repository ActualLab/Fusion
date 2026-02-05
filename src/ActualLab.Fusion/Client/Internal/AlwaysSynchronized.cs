namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Provides a pre-completed <see cref="AsyncTaskMethodBuilder"/> for already-synchronized remote computed values.
/// </summary>
internal static class AlwaysSynchronized
{
    public static readonly AsyncTaskMethodBuilder Source
        = AsyncTaskMethodBuilderExt.New().WithResult();
}
