using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Collector of deferred invalidation blocks. What happens to them, and when,
/// is up to its <see cref="IDeferInvalidationHandler"/>.
/// </summary>
public sealed class DeferInvalidationScope
{
    private static readonly AsyncLocal<DeferInvalidationScope?> CurrentLocal = new();

    private readonly List<Entry> _entries = [];
    // A plain field rather than an AsyncLocal: a harvest is a self-contained operation on this
    // scope, and an AsyncLocal write inside an async method doesn't reliably unwind.
    private List<InvalidationCall>? _recorder;
    private bool _isDiscarded;

    public static DeferInvalidationScope? Current => CurrentLocal.Value;

    public IDeferInvalidationHandler Handler { get; }
    // Resolves the InvalidationMode of the code calling Defer(...); null means "always DefaultMode"
    public Func<InvalidationMode>? ModeResolver { get; init; }
    public InvalidationMode DefaultMode { get; init; } = InvalidationMode.Local;
    public ILogger? Log { get; init; }

    public int Count {
        get {
            lock (_entries)
                return _entries.Count;
        }
    }

    public static DeferInvalidationScopeHandle Begin(IDeferInvalidationHandler? handler = null)
        => Begin(new DeferInvalidationScope(handler));

    public static DeferInvalidationScopeHandle Begin(DeferInvalidationScope scope)
    {
        if (Invalidation.IsActive)
            throw Errors.DeferInvalidationInsideInvalidationPass();

        var oldScope = CurrentLocal.Value;
        CurrentLocal.Value = scope;
        return new DeferInvalidationScopeHandle(scope, oldScope);
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DeferInvalidationScope(IDeferInvalidationHandler? handler = null)
        => Handler = handler ?? DeferInvalidationHandler.Instance;

    public void Add(Func<Task> action)
    {
        var mode = ModeResolver?.Invoke() ?? DefaultMode;
        if (mode is not (InvalidationMode.Local or InvalidationMode.Replicated))
            throw Errors.DeferInvalidationRequiresDeferredMode(mode);

        lock (_entries)
            _entries.Add(new Entry(mode, action));
    }

    public void Discard()
    {
        lock (_entries)
            _isDiscarded = true;
    }

    public bool HasEntries(InvalidationMode mode)
    {
        lock (_entries) {
            foreach (var entry in _entries)
                if (entry.Mode == mode)
                    return true;

            return false;
        }
    }

    public async Task Run(InvalidationSource source, InvalidationMode mode = InvalidationMode.Local)
    {
        var entries = GetEntries(mode);
        if (entries.Length == 0)
            return;

        using var _ = Invalidation.Begin(source);
        foreach (var entry in entries)
            await Invoke(entry).ConfigureAwait(false);
    }

    public async Task<ImmutableList<InvalidationCall>> Harvest()
    {
        var entries = GetEntries(InvalidationMode.Replicated);
        if (entries.Length == 0)
            return ImmutableList<InvalidationCall>.Empty;

        var calls = new List<InvalidationCall>();
        var oldRecorder = Interlocked.Exchange(ref _recorder, calls);
        try {
            using var _ = new ComputeContext(CallOptions.DeferInvalidate).Activate();
            foreach (var entry in entries)
                await Invoke(entry).ConfigureAwait(false);
        }
        finally {
            _recorder = oldRecorder;
        }

        var result = ImmutableList.CreateBuilder<InvalidationCall>();
        var seen = new HashSet<InvalidationCall>();
        foreach (var call in calls)
            if (seen.Add(call))
                result.Add(call);
        return result.ToImmutable();
    }

    // Internal methods

    internal static void Record(InvalidationCall call)
    {
        var recorder = Current?._recorder ?? throw Errors.NoDeferInvalidationRecorder();
        lock (recorder)
            recorder.Add(call);
    }

    internal static void SetCurrent(DeferInvalidationScope? scope)
        => CurrentLocal.Value = scope;

    // Private methods

    private Entry[] GetEntries(InvalidationMode mode)
    {
        lock (_entries) {
            if (_isDiscarded)
                return [];

            var result = new List<Entry>(_entries.Count);
            foreach (var entry in _entries)
                if (entry.Mode == mode)
                    result.Add(entry);
            return result.ToArray();
        }
    }

    private async Task Invoke(Entry entry)
    {
        // A deferred block runs after its mutation committed, so failing it is not an option:
        // the write already landed, and all we can do is report the resulting staleness.
        try {
            await entry.Action.Invoke().ConfigureAwait(false);
        }
        catch (Exception e) {
            FusionInstruments.DeferredInvalidationFailureCount.Add(1);
            Log?.LogError(e, "Deferred invalidation block failed - some computed values stay stale");
        }
    }

    // Nested types

    private readonly record struct Entry(InvalidationMode Mode, Func<Task> Action);
}
