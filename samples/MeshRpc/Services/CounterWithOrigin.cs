using MessagePack;

namespace Samples.MeshRpc.Services;

[MessagePackObject(true)]
[method: SerializationConstructor]
public sealed partial record CounterWithOrigin(
    Counter Counter,
    string Origin)
{
    public override string ToString()
        => $"{Counter} @ {Origin}";
}
