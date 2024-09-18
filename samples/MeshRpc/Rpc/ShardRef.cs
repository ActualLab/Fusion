using System.Runtime.Serialization;
using ActualLab.Mathematics;
using MemoryPack;
using Microsoft.Toolkit.HighPerformance;

namespace Samples.MeshRpc;

[DataContract, MemoryPackable]
public readonly partial record struct ShardRef(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int Key)
{
    public const int ShardCount = MeshSettings.ShardCount;

    public static ShardRef New(object? source)
        => source switch {
            null => default,
            Symbol s => New(s),
            string s => New(s),
            _ => New(source.GetHashCode()),
        };

    public static ShardRef New(int hash)
        => new(hash.PositiveModulo(ShardCount));

    public static ShardRef New(Symbol value)
        => New(value.Value.GetDjb2HashCode().PositiveModulo(ShardCount));

    public static ShardRef New(string value)
        => New(value.GetDjb2HashCode().PositiveModulo(ShardCount));

    public override string ToString()
        => $"shard:{Key}";
}
