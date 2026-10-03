using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Collector of deferred invalidation blocks, all sharing its <see cref="Mode"/>.
/// What happens to them, and when, is up to whoever opened its scope - see
/// <see cref="DeferredInvalidationContextExt"/>.
/// </summary>
/// <remarks>
/// <see cref="Mode"/> starts out null and is decided by the first block added: the mode of the
/// command that's adding it, not of the command this context was opened for. Every later block is
/// checked against it, so a nested handler that needs a different mode fails instead of being
/// silently narrowed - one operation gets one carrier. A handler that defers nothing never has a
/// say, which is what lets a command delegate to handlers whose mode it doesn't know.
/// </remarks>
public sealed class DeferredInvalidationContext
{
    private static readonly AsyncLocal<DeferredInvalidationContext?> CurrentLocal = new();
    private const int ActiveState = 1;
    private const int ClosedState = 2;

    public static DeferredInvalidationContext? Current => CurrentLocal.Value;

    private readonly List<Delegate> _blocks = [];
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    // Lock isn't available here, and nothing locks on _blocks directly
    private object _lock => _blocks;
#endif
    private DeferredInvalidationMode? _mode;
    private int _state; // 0 = new, then ActiveState, then ClosedState

    public DeferredInvalidationMode? Mode { get => _mode; init => _mode = value; }
    public DeferredInvalidationModeResolver? ModeResolver { get; init; }

    public int BlockCount {
        get {
            lock (_lock)
                return _blocks.Count;
        }
    }

    public bool IsClosed {
        get {
            lock (_lock)
                return _state == ClosedState;
        }
    }

    public void AddBlock(Action block)
        => AddBlockImpl(block);
    public void AddBlock(Func<Task> block)
        => AddBlockImpl(block);

    public Scope Activate(bool requireOutermost = true)
    {
        if (Invalidation.IsActive)
            throw Errors.DeferredInvalidationInsideInvalidationPass();

        var oldContext = CurrentLocal.Value;
        if (requireOutermost && oldContext is not null)
            throw Errors.DeferredInvalidationContextIsAlreadyActive();

        lock (_lock) {
            // One context, one activation: its blocks belong to one operation, and reactivating it
            // would either collect them twice or collect them after they were already read
            if (_state != 0)
                throw Errors.DeferredInvalidationContextCannotBeReactivated();

            _state = ActiveState;
        }
        CurrentLocal.Value = this;
        return new Scope(this, oldContext);
    }

    // Nothing is consumed here, so reading the blocks twice yields the same list. Reading them
    // before the scope ends would read less than all there will be, so it's an error rather than a
    // partial answer.
    public Delegate[] GetBlocks()
    {
        lock (_lock) {
            if (_state != ClosedState)
                throw Errors.DeferredInvalidationContextIsNotClosed();

            return _blocks.ToArray();
        }
    }

    // Private methods

    // A block added after the scope ended would never run. Dropping it silently hid real bugs - a
    // block deferred from a Task.Run the handler left behind, or from inside another block.
    private void AddBlockImpl(Delegate block)
    {
        // Resolved outside the lock: it walks the command handler chain, and nothing here needs
        // the lock held for that
        var resolvedMode = ModeResolver?.Resolve();
        lock (_lock) {
            if (_state == ClosedState)
                throw Errors.DeferredInvalidationContextIsClosed();
            if (resolvedMode is { } mode) {
                if (_mode is { } knownMode && knownMode != mode)
                    throw Errors.DeferredInvalidationModeConflict(knownMode, mode);

                _mode = mode;
            }
            else if (_mode is null)
                throw Errors.DeferredInvalidationModeResolverRequired();

            _blocks.Add(block);
        }
    }

    // Nested types

    /// <summary>
    /// A disposable scope of a <see cref="DeferredInvalidationContext"/>: it restores the ambient
    /// context and closes this one. Nothing else happens, and nothing is awaited - what to do with
    /// the collected blocks is up to whoever opened the scope.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly DeferredInvalidationContext? _oldContext;

        public readonly DeferredInvalidationContext? Context;

        internal Scope(DeferredInvalidationContext? context, DeferredInvalidationContext? oldContext)
        {
            Context = context;
            _oldContext = oldContext;
        }

        public void Dispose()
        {
            if (Context is not { } context)
                return;

            lock (context._lock)
                context._state = ClosedState;
            CurrentLocal.Value = _oldContext;
        }
    }
}
