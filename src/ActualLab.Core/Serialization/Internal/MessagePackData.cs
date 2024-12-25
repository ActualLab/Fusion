using MessagePack;

namespace ActualLab.Serialization.Internal;

[StructLayout(LayoutKind.Auto)]
[DataContract, MessagePackObject]
public readonly record struct MessagePackData(
    [property: DataMember(Order = 0), Key(0)] byte[] Data
)
{
    public static implicit operator MessagePackData(byte[] data) => new(data);
}
