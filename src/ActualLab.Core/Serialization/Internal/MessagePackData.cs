using MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A lightweight wrapper around a byte array representing raw MessagePack-serialized data.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MessagePackFormatter(typeof(MessagePackDataMessagePackFormatter))]
public readonly record struct MessagePackData(
    [property: DataMember(Order = 0), Key(0)] byte[] Data)
{
    public static implicit operator MessagePackData(byte[] data) => new(data);
}
