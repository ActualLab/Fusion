using ActualLab.OS;

namespace ActualLab.Time;

public record ConcurrentFixedTimerSetOptions : FixedTimerSetOptions
{
    public static new readonly ConcurrentFixedTimerSetOptions Default = new();

    public int ConcurrencyLevel { get; init; } = HardwareInfo.GetProcessorCountPo2Factor();
}

public sealed class ConcurrentFixedTimerSet<TItem> : SafeAsyncDisposableBase
    where TItem : notnull
{
    private readonly FixedTimerSet<TItem>[] _timerSets;
    private readonly int _concurrencyLevelMask;

    public MomentClock Clock { get; }
    public TickSource TickSource { get; }
    public TimeSpan FireInterval { get; }
    public int ConcurrencyLevel { get; }

    public int Count => _timerSets.Sum(s => s.Count);

    public ConcurrentFixedTimerSet(ConcurrentFixedTimerSetOptions options, Action<TItem>? fireHandler = null)
    {
        Clock = options.Clock;
        TickSource = options.TickSource;
        FireInterval = options.FireDelay;
        ConcurrencyLevel = (int)Bits.GreaterOrEqualPowerOf2((ulong)Math.Max(1, options.ConcurrencyLevel));
        _concurrencyLevelMask = ConcurrencyLevel - 1;
        _timerSets = new FixedTimerSet<TItem>[ConcurrencyLevel];
        for (var i = 0; i < _timerSets.Length; i++)
            _timerSets[i] = new FixedTimerSet<TItem>(options, fireHandler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TItem item)
        => GetTimerSet(item).Add(item);

    public Task FireImmediately()
    {
        var tasks = new List<Task>(_timerSets.Length);
        foreach (var timerSet in _timerSets)
            tasks.Add(timerSet.FireImmediately());
        return Task.WhenAll(tasks);
    }

    protected override Task DisposeAsync(bool disposing)
    {
        if (!disposing)
            return Task.CompletedTask;

        var tasks = new List<Task>(_timerSets.Length);
        foreach (var timerSet in _timerSets)
            tasks.Add(timerSet.DisposeAsync().AsTask());
        return Task.WhenAll(tasks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FixedTimerSet<TItem> GetTimerSet(TItem item)
        => _timerSets[item.GetHashCode() & _concurrencyLevelMask];
}
