namespace ActualLab.Collections.Slim;

/// <summary>
/// A compact hash set interface for reference types, optimized for small item counts
/// using reference equality before falling back to a full <see cref="HashSet{T}"/>.
/// </summary>
public interface IRefHashSetSlim<T>
    where T : class
{
    public int Count { get; }
    public IEnumerable<T> Items { get; }

    public bool Contains(T item);
    public bool Add(T item);
    public bool Remove(T item);
    public void Clear();

    public void Apply<TState>(TState state, Action<TState, T> action);
    public void Aggregate<TState>(ref TState state, Aggregator<TState, T> aggregator);
    public TState Aggregate<TState>(TState state, Func<TState, T, TState> aggregator);
    public void CopyTo(Span<T> target);
}
