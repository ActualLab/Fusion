using ActualLab.IO;

namespace ActualLab.Flows.Infrastructure;

public class FlowSerializer
{
    public IByteSerializer ByteSerializer { get; init; } = TypeDecoratingByteSerializer.Default;

    public virtual byte[]? Serialize(Flow? flow)
    {
        if (ReferenceEquals(flow, null))
            return null;

        using var buffer = new ArrayPoolBuffer<byte>(256);
        ByteSerializer.Write(buffer, flow, flow.GetType());
        return buffer.WrittenSpan.ToArray();
    }

    public virtual Flow? Deserialize(byte[]? data)
        => data == null || data.Length == 0 ? null
            : ByteSerializer.Read<Flow?>(data);

    public virtual byte[]? SerializeEvent(object? @event)
    {
        if (ReferenceEquals(@event, null))
            return null;

        using var buffer = new ArrayPoolBuffer<byte>(256);
        ByteSerializer.Write(buffer, @event, @event.GetType());
        return buffer.WrittenSpan.ToArray();
    }

    public virtual object? DeserializeEvent(byte[]? data)
        => data == null || data.Length == 0 ? null
            : ByteSerializer.Read<object?>(data);
}
