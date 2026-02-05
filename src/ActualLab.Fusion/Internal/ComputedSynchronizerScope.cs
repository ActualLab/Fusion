namespace ActualLab.Fusion.Internal;

/// <summary>
/// A disposable scope that temporarily replaces the ambient <see cref="ComputedSynchronizer"/>.
/// </summary>
public readonly struct ComputedSynchronizerScope : IDisposable
{
    private readonly ComputedSynchronizer? _oldSynchronizer;

    public readonly ComputedSynchronizer? Synchronizer;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ComputedSynchronizerScope(ComputedSynchronizer synchronizer)
    {
        _oldSynchronizer = ComputedSynchronizer.CurrentLocal.Value;
        Synchronizer = synchronizer;
        if (!ReferenceEquals(_oldSynchronizer, synchronizer))
            ComputedSynchronizer.CurrentLocal.Value = synchronizer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!ReferenceEquals(_oldSynchronizer, Synchronizer))
            ComputedSynchronizer.CurrentLocal.Value = _oldSynchronizer!;
    }
}
