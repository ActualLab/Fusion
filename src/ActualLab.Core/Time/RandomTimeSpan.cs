using ActualLab.Generators;
using MessagePack;

namespace ActualLab.Time;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
[DataContract, MemoryPackable, MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, SerializationConstructor]
// Unmanaged struct, so no need for [method: MemoryPackConstructor]
public readonly partial record struct RandomTimeSpan(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] TimeSpan Origin,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] TimeSpan MaxDelta = default
) {
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public TimeSpan Min => (Origin - MaxDelta).Positive();
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public TimeSpan Max => (Origin + MaxDelta).Positive();

    public RandomTimeSpan(TimeSpan origin, double maxDelta)
        : this(origin, TimeSpan.FromSeconds(maxDelta * origin.TotalSeconds))
    { }

    public RandomTimeSpan(double originInSeconds, double maxDelta = default)
        : this(TimeSpan.FromSeconds(originInSeconds), TimeSpan.FromSeconds(maxDelta * originInSeconds))
    { }

    // Conversion

    public override string ToString()
        => MaxDelta <= TimeSpan.Zero
            ? Origin.ToString()
            : $"({Origin.ToShortString()} ± {MaxDelta.ToShortString()})";

    public static implicit operator RandomTimeSpan(TimeSpan origin) => new(origin);
    public static implicit operator RandomTimeSpan(double originInSeconds) => new(originInSeconds);
    public static implicit operator RandomTimeSpan((TimeSpan Origin, TimeSpan MaxDelta) pair) => new(pair.Origin, pair.MaxDelta);
    public static implicit operator RandomTimeSpan((double OriginInSeconds, double MaxDelta) pair) => new(pair.OriginInSeconds, pair.MaxDelta);

    public TimeSpan Next()
    {
        if (MaxDelta <= TimeSpan.Zero)
            return Origin;

        var delta = MaxDelta.MultiplyBy(2 * (RandomShared.NextDouble() - 0.5));
        return (Origin + delta).Positive();
    }
}
