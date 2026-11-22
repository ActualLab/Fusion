using MessagePack;

namespace Samples.MeshRpc.Services;

[MessagePackObject(true)]
[method: SerializationConstructor]
public sealed partial record Counter(
    int Key,
    int Value,
    CpuTimestamp UpdatedAt)
{
    public Counter(int key, int value)
        : this(key, value, CpuTimestamp.Now)
    { }

    public override string ToString()
        => $"{Key} = {Value}, updated {UpdatedAt.Elapsed.ToShortString()} ago";
}
