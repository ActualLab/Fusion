using ActualLab.IO;

namespace ActualLab.Flows.Infrastructure;

public class FlowSerializer
{
    public IByteSerializer ByteSerializer { get; init; } = TypeDecoratingByteSerializer.Default;

    public virtual byte[] Serialize(Flow flow)
    {
        using var buffer = new ArrayPoolBuffer<byte>(256);
        ByteSerializer.Write(buffer, flow, flow.GetType());
        return buffer.WrittenSpan.ToArray();
    }

    public virtual Flow Deserialize(byte[] data)
        => ByteSerializer.Read<Flow>(data);
}
