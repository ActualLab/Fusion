namespace ActualLab.Fusion.Client.Internal;

public readonly struct RemoteComputeSynchronizerScope : IDisposable
{
    private readonly IRemoteComputedSynchronizer? _oldSynchronizer;

    public readonly IRemoteComputedSynchronizer? Synchronizer;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal RemoteComputeSynchronizerScope(IRemoteComputedSynchronizer? synchronizer)
    {
        _oldSynchronizer = RemoteComputedSynchronizer.Current;
        Synchronizer = synchronizer;
        if (!ReferenceEquals(_oldSynchronizer, synchronizer))
            RemoteComputedSynchronizer.Current = synchronizer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!ReferenceEquals(_oldSynchronizer, Synchronizer))
            RemoteComputedSynchronizer.Current = _oldSynchronizer;
    }
}
