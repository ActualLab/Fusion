namespace ActualLab.Fusion.Tests.Services;

public class CounterSumService : IComputeService
{
    private readonly IMutableState<int>[] _counters;

    public IMutableState<int> this[int counterIndex] => _counters[counterIndex];

    public CounterSumService(IStateFactory stateFactory)
        => _counters = Enumerable.Range(0, 10)
            .Select(_ => stateFactory.NewMutable<int>())
            .ToArray();

    [ComputeMethod]
    public virtual async Task<int> Get0(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);

    [ComputeMethod(InvalidationDelay = 0.2)]
    public virtual async Task<int> Get1(int counterIndex, CancellationToken cancellationToken = default)
        => await this[counterIndex].Use(cancellationToken);

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
        return t1.Result + t2.Result;
#pragma warning restore VSTHRD103
    }
}
