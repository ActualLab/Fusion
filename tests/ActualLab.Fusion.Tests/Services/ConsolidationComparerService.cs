namespace ActualLab.Fusion.Tests.Services;

// A reference type with the default (referential) Equals - i.e. one that can never
// consolidate unless a custom ConsolidationComparer is provided.
public class ValueBox(int value)
{
    public int Value { get; } = value;
}

public class ValueBoxComparer : IEqualityComparer<ValueBox>
{
    private static int _instanceCount;

    public static int InstanceCount => Volatile.Read(ref _instanceCount);

    public ValueBoxComparer()
        => Interlocked.Increment(ref _instanceCount);

    public bool Equals(ValueBox? x, ValueBox? y)
        => x is null || y is null ? x is null && y is null : x.Value == y.Value;

    public int GetHashCode(ValueBox obj)
        => obj.Value;
}

public class ParityComparer : IEqualityComparer<int>
{
    public bool Equals(int x, int y)
        => (x & 1) == (y & 1);

    public int GetHashCode(int obj)
        => obj & 1;
}

public class NeverEqualComparer : IEqualityComparer<int>
{
    private static int _callCount;

    public static int CallCount => Volatile.Read(ref _callCount);

    public bool Equals(int x, int y)
    {
        Interlocked.Increment(ref _callCount);
        return false;
    }

    public int GetHashCode(int obj)
        => obj;
}

public class NoParameterlessCtorComparer(int seed) : IEqualityComparer<int>
{
    public bool Equals(int x, int y)
        => x + seed == y + seed;

    public int GetHashCode(int obj)
        => obj;
}

public class WrongValueTypeComparerService : IComputeService
{
    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(ParityComparer))]
    public virtual Task<string> Get()
        => Task.FromResult("");
}

public class NoParameterlessCtorComparerService : IComputeService
{
    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(NoParameterlessCtorComparer))]
    public virtual Task<int> Get()
        => Task.FromResult(0);
}

public class ComparerWithoutDelayService : IComputeService
{
    [ComputeMethod(ConsolidationComparer = typeof(ParityComparer))]
    public virtual Task<int> Get()
        => Task.FromResult(0);
}

public interface IRemoteComparerCounter : IComputeService
{
    [RemoteComputeMethod(ConsolidationComparer = typeof(ParityComparer))]
    Task<int> Get(CancellationToken cancellationToken = default);
}

public interface IRpcComparerCounter : IComputeService
{
    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(ParityComparer))]
    Task<int> Get(CancellationToken cancellationToken = default);
}

public class RpcComparerCounter : IRpcComparerCounter
{
    public virtual Task<int> Get(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

public class ConsolidationComparerService(StateFactory stateFactory) : IComputeService
{
    public MutableState<int> LooseSource { get; } = stateFactory.NewMutable<int>();
    public MutableState<int> TightSource { get; } = stateFactory.NewMutable<int>();
    public MutableState<int> BoxSource { get; } = stateFactory.NewMutable<int>();
    public MutableState<int> ErrorSource { get; } = stateFactory.NewMutable<int>();

    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(ParityComparer))]
    public virtual async Task<int> GetLoose(CancellationToken cancellationToken = default)
        => await LooseSource.Use(cancellationToken);

    [ComputeMethod(ConsolidationDelay = 0)]
    public virtual async Task<int> GetTightWithDefaultComparer(CancellationToken cancellationToken = default)
        => await TightSource.Use(cancellationToken);

    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(NeverEqualComparer))]
    public virtual async Task<int> GetTight(CancellationToken cancellationToken = default)
        => await TightSource.Use(cancellationToken);

    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(ValueBoxComparer))]
    public virtual async Task<ValueBox?> GetBox(CancellationToken cancellationToken = default)
    {
        var value = await BoxSource.Use(cancellationToken);
        return value < 0 ? null : new ValueBox(value);
    }

    [ComputeMethod(ConsolidationDelay = 0)]
    public virtual async Task<ValueBox?> GetBoxWithDefaultComparer(CancellationToken cancellationToken = default)
    {
        var value = await BoxSource.Use(cancellationToken);
        return value < 0 ? null : new ValueBox(value);
    }

    [ComputeMethod(ConsolidationDelay = 0, ConsolidationComparer = typeof(NeverEqualComparer))]
    public virtual async Task<int> GetFailing(CancellationToken cancellationToken = default)
    {
        await ErrorSource.Use(cancellationToken);
        throw new InvalidOperationException("Always fails.");
    }
}
