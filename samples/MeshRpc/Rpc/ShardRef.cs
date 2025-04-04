using System.Runtime.Serialization;
using ActualLab.Mathematics;
using MemoryPack;
using MessagePack;

namespace Samples.MeshRpc;

[DataContract, MemoryPackable, MessagePackObject(true)]
public readonly partial record struct ShardRef(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int Key)
{
    public const int ShardCount = MeshSettings.ShardCount;

    public static ShardRef New(object? source)
        => source switch {
            null => default,
            string s => New(s),
            _ => New(source.GetHashCode()),
        };

    public static ShardRef New(int hash)
        => new(hash.PositiveModulo(ShardCount));

    public static ShardRef New(string value)
        => New(value.GetXxHash3().PositiveModulo(ShardCount));

    public override string ToString()
        => $"shard:{Key}";
}
