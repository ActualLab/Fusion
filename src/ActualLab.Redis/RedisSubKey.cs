using StackExchange.Redis;

namespace ActualLab.Redis;

[StructLayout(LayoutKind.Auto)]
[Serializable]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct RedisSubKey
{
    private readonly string _key;
    private readonly RedisChannel.PatternMode _patternMode;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public string Key { get; }

    [DataMember(Order = 1), MemoryPackOrder(1)]
    public RedisChannel.PatternMode PatternMode { get; }

    public RedisSubKey(string key) : this(key, RedisChannel.PatternMode.Auto) { }

    [Newtonsoft.Json.JsonConstructor, JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RedisSubKey(string key, RedisChannel.PatternMode patternMode)
    {
        _key = key;
        _patternMode = patternMode;
        Key = key;
        PatternMode = patternMode;
    }

    public override string ToString()
        => $"`{Key}`, {PatternMode})";

    public static implicit operator RedisSubKey(string key) => new(key);
    public static implicit operator RedisSubKey((string Key, RedisChannel.PatternMode PatternMode) pair)
        => new(pair.Key, pair.PatternMode);
}
