using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

public interface IMutablePropertyBag : IReadOnlyPropertyBag
{
    public new object? this[string key] { get; set; }
    public new object? this[Type key] { get; set; }

    public event Action? Changed;

    public bool Set<T>(T value);
    public bool Set<T>(string key, T value);
    public bool Set(string key, object? value);
    public bool Set(Type key, object? value);
    public void SetMany(PropertyBag items);
    public void SetMany(params ReadOnlySpan<PropertyBagItem> items);
    public bool Remove<T>();
    public bool Remove(string key);
    public bool Remove(Type key);
    public void Clear();

    public bool Update(PropertyBag bag);
    public bool Update(Func<PropertyBag, PropertyBag> updater);
    public bool Update<TState>(TState state, Func<TState, PropertyBag, PropertyBag> updater);
}

#pragma warning disable CS0618 // Type or member is obsolete

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial class MutablePropertyBag : IMutablePropertyBag
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private PropertyBag _snapshot;

    public event Action? Changed;

    // MessagePack requires this member to be public
    [Obsolete("This member exists solely to make serialization work. Don't use it!")]
    [DataMember(Order = 0), MemoryPackOrder(0), Key(0), MemoryPackInclude, JsonInclude, Newtonsoft.Json.JsonProperty]
    public PropertyBagItem[]? RawItems {
        get => _snapshot.RawItems;
        init => _snapshot = new PropertyBag(value);
    }

    // Computed properties

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public PropertyBag Snapshot {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot;
        set => Update(value, (bag, _) => bag);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.Count;
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public IReadOnlyList<PropertyBagItem> Items {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.Items;
    }

    public object? this[string key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot[key];
        set => Update((key, value), static (s, bag) => bag.Set(s.key, s.value));
    }

    public object? this[Type key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot[key.ToIdentifierSymbol()];
        set => Update((key, value), static (s, bag) => bag.Set(s.key.ToIdentifierSymbol(), s.value));
    }

    public MutablePropertyBag()
    { }

    public MutablePropertyBag(PropertyBag snapshot)
        => _snapshot = snapshot;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public MutablePropertyBag(PropertyBagItem[]? rawItems)
        => _snapshot = new PropertyBag(rawItems);

    public override string ToString()
        => $"{nameof(MutablePropertyBag)}({PropertyBagHelper.GetToStringArgs(RawItems)})";

    // Contains

    public bool Contains<T>()
        => _snapshot[typeof(T).ToIdentifierSymbol()] != null;

    public bool Contains(string key)
        => _snapshot[key] != null;

    public bool Contains(Type key)
        => _snapshot[key.ToIdentifierSymbol()] != null;

    // TryGet

    public bool TryGet<T>([MaybeNullWhen(false)] out T value)
        => _snapshot.TryGet(typeof(T).ToIdentifierSymbol(), out value);

    public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value)
        => _snapshot.TryGet(key, out value);

    // Get

    public T? Get<T>()
        where T : class
        => _snapshot.Get<T>(typeof(T).ToIdentifierSymbol());

    public T? Get<T>(string key)
        where T : class
        => (T?)_snapshot[key];

    // GetOrDefault

    public T GetOrDefault<T>()
        => _snapshot.GetOrDefault<T>(typeof(T).ToIdentifierSymbol());

    public T GetOrDefault<T>(string key)
        => _snapshot.GetOrDefault<T>(key);

    public T GetOrDefault<T>(T @default)
        => _snapshot.GetOrDefault(typeof(T).ToIdentifierSymbol(), @default);

    public T GetOrDefault<T>(string key, T @default)
        => _snapshot.GetOrDefault(key, @default);

    // Set

    public bool Set<T>(T value)
        => Set(typeof(T).ToIdentifierSymbol(), (object?)value);

    public bool Set<T>(string key, T value)
        => Set(key, (object?)value);

    public bool Set(string key, object? value)
        => Update((key, value), static (s, bag) => bag.Set(s.key, s.value));

    public bool Set(Type key, object? value)
        => Update((key, value), static (s, bag) => bag.Set(s.key.ToIdentifierSymbol(), s.value));

    // SetMany

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

    public bool Remove<T>()
        => Remove(typeof(T).ToIdentifierSymbol());

    public bool Remove(string key)
        => Update(key, static (k, bag) => bag.Remove(k));

    public bool Remove(Type key)
        => Update(key, static (k, bag) => bag.Remove(k.ToIdentifierSymbol()));

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
