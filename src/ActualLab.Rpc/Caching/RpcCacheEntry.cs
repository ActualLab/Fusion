namespace ActualLab.Rpc.Caching;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial class RpcCacheEntry
{
    private readonly RpcCacheKey _key;
    private readonly TextOrBytes _data;

    [DataMember(Order = 0), MemoryPackOrder(0)] public RpcCacheKey Key { get; init; }
    [DataMember(Order = 1), MemoryPackOrder(1)] public TextOrBytes Data { get; init; }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcCacheEntry(RpcCacheKey key, TextOrBytes data)
    {
        _key = key;
        _data = data;
        Key = key;
        Data = data;
    }

    public void Deconstruct(out RpcCacheKey key, out TextOrBytes data)
    {
        key = Key;
        data = Data;
    }

    public override string ToString()
        => $"{nameof(RpcCacheEntry)}({Key} -> {Data.ToString(16)})";
}
