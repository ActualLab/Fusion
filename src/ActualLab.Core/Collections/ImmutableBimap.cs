namespace ActualLab.Collections;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial record ImmutableBimap<TFrom, TTo>
    where TFrom : notnull
    where TTo : notnull
{
    private readonly IReadOnlyDictionary<TFrom, TTo> _forward = ImmutableDictionary<TFrom, TTo>.Empty;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public IReadOnlyDictionary<TFrom, TTo> Forward {
        get => _forward;
        init {
            _forward = value;
            Backward = value.Count == 0
                ? ImmutableDictionary<TTo, TFrom>.Empty
                : Forward.ToDictionary(kv => kv.Value, kv => kv.Key);
        }
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public IReadOnlyDictionary<TTo, TFrom> Backward { get; private set; } = ImmutableDictionary<TTo, TFrom>.Empty;

    public ImmutableBimap()
    { }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ImmutableBimap(IReadOnlyDictionary<TFrom, TTo> forward)
        => Forward = forward;

    public ImmutableBimap(IReadOnlyDictionary<TFrom, TTo> forward, IReadOnlyDictionary<TTo, TFrom> backward)
    {
        _forward = forward;
        Backward = backward;
    }

    // Equality
    public virtual bool Equals(ImmutableBimap<TFrom, TTo>? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return Forward.Equals(other.Forward);
    }

    public override int GetHashCode() => Forward.GetHashCode();
}
