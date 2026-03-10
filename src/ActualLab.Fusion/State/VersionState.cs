namespace ActualLab.Fusion;

public class VersionState : MutableState<long>
{
    public new record Options : MutableState<long>.Options;

    public VersionState(Options options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize: false)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(options);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Increment(new InvalidationSource(file, member, line));

    public void Increment(InvalidationSource source)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            var computed = Unsafe.As<Computed<long>>(snapshot.LastNonErrorComputed);
            NextOutput = new Result(computed.Value + 1);
            snapshot.Computed.Invalidate(source);
        }
    }
}
