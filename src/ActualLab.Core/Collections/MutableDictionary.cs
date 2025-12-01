namespace ActualLab.Collections;

public interface IReadOnlyMutableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public ImmutableDictionary<TKey, TValue> Items { get; }
    public event Action? Changed;
}

// ReSharper disable once PossibleInterfaceMemberAmbiguity
public interface IMutableDictionary<TKey, TValue> : IReadOnlyMutableDictionary<TKey, TValue>, IDictionary<TKey, TValue>
    where TKey : notnull
{
    public new ImmutableDictionary<TKey, TValue> Items { get; set; }

    public bool Update(ImmutableDictionary<TKey, TValue> items);
    public bool Update(ImmutableDictionary<TKey, TValue> items, ImmutableDictionary<TKey, TValue> expectedItems);
    public bool Update<TState>(TState state, Func<TState, ImmutableDictionary<TKey, TValue>, ImmutableDictionary<TKey, TValue>> updater);
}

public class MutableDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue> items)
    : IMutableDictionary<TKey, TValue>
    where TKey : notnull
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private volatile ImmutableDictionary<TKey, TValue> _items = items;

    public ImmutableDictionary<TKey, TValue> Items {
        get => _items;
        set => Update(value);
    }

    public event Action? Changed;

    public int Count => _items.Count;

    public TValue this[TKey key] {
        get => _items[key];
        set => Update((key, value), static (v, items) => items.SetItem(v.key, v.value));
    }

    public IEnumerable<TKey> Keys => _items.Keys;
    public IEnumerable<TValue> Values => _items.Values;
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => throw new NotSupportedException();
    ICollection<TValue> IDictionary<TKey, TValue>.Values => throw new NotSupportedException();
    public bool IsReadOnly => false;

    public MutableDictionary() : this(ImmutableDictionary<TKey, TValue>.Empty) { }

    public override string ToString()
        => $"{GetType().GetName()}({Count} item(s))";

    public bool Update(ImmutableDictionary<TKey, TValue> items)
    {
        lock (_lock) {
            if (_items == items)
                return false;

            _items = items;
        }
        Changed?.Invoke();
        return true;
    }

    public bool Update(ImmutableDictionary<TKey, TValue> items, ImmutableDictionary<TKey, TValue> expectedItems)
    {
        lock (_lock) {
            if (_items != expectedItems || _items == items)
                return false;

            _items = items;
        }
        Changed?.Invoke();
        return true;
    }

    public bool Update(Func<ImmutableDictionary<TKey, TValue>, ImmutableDictionary<TKey, TValue>> updater)
    {
        lock (_lock) {
            var items = _items;
            var newItems = updater.Invoke(items);
            if (newItems == items)
                return false;

            _items = newItems;
        }
        Changed?.Invoke();
        return true;
    }

    public bool Update<TState>(TState state, Func<TState, ImmutableDictionary<TKey, TValue>, ImmutableDictionary<TKey, TValue>> updater)
    {
        lock (_lock) {
            var items = _items;
            var newItems = updater.Invoke(state, items);
            if (newItems == items)
                return false;

            _items = newItems;
        }
        Changed?.Invoke();
        return true;
    }

    // IReadOnlyDictionary members

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => Items.GetEnumerator();
    public bool ContainsKey(TKey key)
        => Items.ContainsKey(key);
#pragma warning disable CS8767
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        => Items.TryGetValue(key, out value);
#pragma warning restore CS8767

    // IDictionary members

    public void Add(TKey key, TValue value)
        => Update((key, value), static (v, items) => items.Add(v.key, v.value));
    public void Add(KeyValuePair<TKey, TValue> item)
        => Update(item, static (v, items) => items.Add(v.Key, v.Value));
    public bool Contains(KeyValuePair<TKey, TValue> item)
        => Items.Contains(item);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => Items.ToList().CopyTo(array, arrayIndex); // Sub-optimal, but who's gonna use it?
    public bool Remove(KeyValuePair<TKey, TValue> item)
        => Update(item, static (v, items) => items.Contains(v) ? items.Remove(v.Key) : items);
    public bool Remove(TKey key)
        => Update(key, static (k, items) => items.Remove(k));
    public void Clear()
        => Update(ImmutableDictionary<TKey, TValue>.Empty);
}
