using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Collections;

#pragma warning disable CS0618 // Type or member is obsolete

/// <summary>
/// A thread-safe mutable property bag backed by an immutable <see cref="PropertyBag"/>
/// with atomic update operations and change notifications.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial class MutablePropertyBag
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

    // SetMany

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
