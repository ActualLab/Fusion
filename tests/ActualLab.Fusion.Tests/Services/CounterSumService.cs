namespace ActualLab.Fusion.Tests.Services;

public class CounterSumService : IComputeService
{
    public static readonly AsyncLocal<string?> AmbientTag = new();

    private readonly MutableState<int>[] _counters;

    public string? LastGetC0Tag;
    public int GetC0CallCount;

    public MutableState<int> this[int counterIndex] => _counters[counterIndex];

    // ReSharper disable once ConvertToPrimaryConstructor
    public CounterSumService(StateFactory stateFactory)
        => _counters = Enumerable.Range(0, 10)
            .Select(_ => stateFactory.NewMutable<int>())
            .ToArray();

    [ComputeMethod]
    public virtual async Task<int> Get0(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);

    [ComputeMethod(InvalidationDelay = 0.2)]
    public virtual async Task<int> Get1(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);

    [ComputeMethod(ConsolidationDelay = 0)]
    public virtual async Task<int> GetC0(int counterIndex, CancellationToken cancellationToken = default)
    {
        LastGetC0Tag = AmbientTag.Value;
        Interlocked.Increment(ref GetC0CallCount);
        return await this[counterIndex].Use(cancellationToken);
    }

    [ComputeMethod(ConsolidationDelay = 0.2)]
    public virtual async Task<int> GetC2(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);

    [ComputeMethod]
    public virtual async Task<int> GetViaProtectedC0(int counterIndex, CancellationToken cancellationToken = default)
        => await GetProtectedC0(counterIndex, cancellationToken);

    public Task<Computed<int>> CaptureProtectedC0(int counterIndex)
        => Computed.Capture(() => GetProtectedC0(counterIndex, default)).AsTask();

    [ComputeMethod]
    public virtual async Task<int> Sum(
        int counterIndex1,
        int counterIndex2,
        CancellationToken cancellationToken = default)
    {
        var t1 = Get0(counterIndex1, cancellationToken);
        var t2 = Get1(counterIndex2, cancellationToken);
        await Task.WhenAll(t1, t2);
#pragma warning disable VSTHRD103
        return t1.GetAwaiter().GetResult() + t2.GetAwaiter().GetResult();
#pragma warning restore VSTHRD103
    }

    // Protected methods

    [ComputeMethod(ConsolidationDelay = 0)]
    protected virtual async Task<int> GetProtectedC0(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);
}
