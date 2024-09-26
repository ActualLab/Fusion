using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

public interface IMutablePropertyBag : IReadOnlyPropertyBag
{
    event Action? Changed;

    bool Set<T>(T value);
    bool Set<T>(Symbol key, T value);
    bool Set(Symbol key, object? value);
    void SetMany(PropertyBag items);
    void SetMany(params ReadOnlySpan<PropertyBagItem> items);
    bool Remove<T>();
    bool Remove(Symbol key);
    void Clear();

    bool Update(PropertyBag bag);
    bool Update(Func<PropertyBag, PropertyBag> updater);
    bool Update<TState>(TState state, Func<TState, PropertyBag, PropertyBag> updater);
}

#pragma warning disable CS0618 // Type or member is obsolete

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial class MutablePropertyBag : IMutablePropertyBag
{
    private readonly Lock _lock = new();
    private PropertyBag _snapshot;

    public event Action? Changed;

    // MessagePack requires this member to be public
    [Obsolete("This member exists solely to make serialization work. Don't use it!")]
    [DataMember(Order = 0), MemoryPackOrder(0), MemoryPackInclude, JsonInclude, Newtonsoft.Json.JsonProperty]
    public PropertyBagItem[]? RawItems {
        get => _snapshot.RawItems;
        init => _snapshot = new PropertyBag(value);
    }

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public PropertyBag Snapshot {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot;
        set => Update(value, (bag, _) => bag);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.Count;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public IReadOnlyList<PropertyBagItem> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.Items;
    }

    public object? this[Symbol key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot[key];
        set => Update((key, value), static (s, bag) => bag.Set(s.key, s.value));
    }

    public MutablePropertyBag()
    { }

    public MutablePropertyBag(PropertyBag snapshot)
        => _snapshot = snapshot;

    [MemoryPackConstructor, JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public MutablePropertyBag(PropertyBagItem[]? rawItems)
        => _snapshot = new PropertyBag(rawItems);

    public override string ToString()
        => $"{nameof(MutablePropertyBag)}({PropertyBagHelper.GetToStringArgs(RawItems)})";

    // Contains

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains<T>()
        => _snapshot[typeof(T)] != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Symbol key)
        => _snapshot[key] != null;

    // TryGet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>([MaybeNullWhen(false)] out T value)
        => _snapshot.TryGet(typeof(T), out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(Symbol key, [MaybeNullWhen(false)] out T value)
        => _snapshot.TryGet(key, out value);

    // Get

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? Get<T>()
        where T : class
        => _snapshot.Get<T>(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? Get<T>(Symbol key)
        where T : class
        => (T?)_snapshot[key];

    // GetOrDefault

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>()
        => _snapshot.GetOrDefault<T>(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>(Symbol key)
        => _snapshot.GetOrDefault<T>(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>(T @default)
        => _snapshot.GetOrDefault(typeof(T), @default);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrDefault<T>(Symbol key, T @default)
        => _snapshot.GetOrDefault(key, @default);

    // Set

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set<T>(T value)
        => Set(typeof(T), (object?)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set<T>(Symbol key, T value)
        => Set(key, (object?)value);

    public bool Set(Symbol key, object? value)
        => Update((key, value), static (s, bag) => bag.Set(s.key, s.value));

    // SetMany

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMany(PropertyBag items)
        => SetMany(items.RawItems ?? []);

    public void SetMany(params ReadOnlySpan<PropertyBagItem> items)
    {
        bool isChanged;
        lock (_lock) {
            var oldSnapshot = _snapshot;
            _snapshot = oldSnapshot.SetMany(items);
            isChanged = _snapshot != oldSnapshot;
        }
        if (isChanged)
            Changed?.Invoke();
    }

    // Remove

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<T>()
        => Remove(typeof(T));

    public bool Remove(Symbol key)
        => Update(key, static (k, bag) => bag.Remove(k));

    // Clear

    public void Clear()
        => Update(_ => default);

    // Update

    public bool Update(PropertyBag bag)
    {
        lock (_lock) {
            if (_snapshot == bag)
                return false;

            _snapshot = bag;
        }
        Changed?.Invoke();
        return true;
    }

    public bool Update(Func<PropertyBag, PropertyBag> updater)
    {
        lock (_lock) {
            var bag = updater.Invoke(_snapshot);
            if (_snapshot == bag)
                return false;

            _snapshot = bag;
        }
        Changed?.Invoke();
        return true;
    }

    public bool Update<TState>(TState state, Func<TState, PropertyBag, PropertyBag> updater)
    {
        lock (_lock) {
            var bag = updater.Invoke(state, _snapshot);
            if (_snapshot == bag)
                return false;

            _snapshot = bag;
        }
        Changed?.Invoke();
        return true;
    }
}
