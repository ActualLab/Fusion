using MessagePack;
using StackExchange.Redis;

namespace ActualLab.Redis;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: Newtonsoft.Json.JsonConstructor, JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public readonly partial record struct RedisSubKey(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] RedisChannel.PatternMode PatternMode
) {
    public RedisSubKey(string key) : this(key, RedisChannel.PatternMode.Auto) { }

    public override string ToString()
        => $"`{Key}`, {PatternMode}";

    public static implicit operator RedisSubKey(string key) => new(key);
    public static implicit operator RedisSubKey((string Key, RedisChannel.PatternMode PatternMode) pair)
        => new(pair.Key, pair.PatternMode);
}
