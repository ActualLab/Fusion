namespace Samples.MeshRpc.Services;

public static class CounterStorage
{
    private static readonly object Lock = new();
    private static readonly Dictionary<int, MutableState<Counter>> States = new();

    public static Counter Get(int key)
    {
        MutableState<Counter>? state;
        lock (Lock)
            state = GetOrCreateStateFromLock(key);
        return state.Value;
    }

    public static Task<Counter> Use(int key, CancellationToken cancellationToken = default)
    {
        MutableState<Counter>? state;
        lock (Lock)
            state = GetOrCreateStateFromLock(key);
        return state.Use(cancellationToken);
    }

    public static Counter Increment(int key)
    {
        lock (Lock) {
            var state = GetOrCreateStateFromLock(key);
            state.Set(r => new Counter(key, r.Value.Value + 1));
            return state.Value;
        }
    }

    // Private methods

    private static MutableState<Counter> GetOrCreateStateFromLock(int key)
        => States.GetValueOrDefault(key) ?? (States[key] = StateFactory.Default.NewMutable(new Counter(key, 0)));
}
