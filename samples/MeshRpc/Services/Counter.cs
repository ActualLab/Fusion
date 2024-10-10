using MemoryPack;
using MessagePack;

namespace Samples.MeshRpc.Services;

[MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[method: MemoryPackConstructor, SerializationConstructor]
public sealed partial record Counter(
    [property: MemoryPackOrder(0), Key(0)] int Key,
    [property: MemoryPackOrder(1), Key(1)] int Value,
    [property: MemoryPackOrder(2), Key(2)] CpuTimestamp UpdatedAt)
{
    public Counter(int key, int value)
        : this(key, value, CpuTimestamp.Now)
    { }

    public override string ToString()
        => $"{Key} = {Value}, updated {UpdatedAt.Elapsed.ToShortString()} ago";
}
