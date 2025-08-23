using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteArgumentSerializerV3(IByteSerializer baseSerializer) : RpcArgumentSerializer(false)
{
    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint)
    {
        var buffer = GetWriteBuffer(sizeHint);
        if (needsPolymorphism) // Rare case
            return PolySerialize(arguments, buffer);

        // Frequent case
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken))
                continue;

            var item = arguments.GetUntyped(i);
            baseSerializer.Write(buffer, item, type);
        }
        return GetWriteBufferMemory(buffer);
    }

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
    {
        if (needsPolymorphism) { // Rare case
            PolyDeserialize(arguments, data);
            return;
        }

        // Frequent case
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken)) {
                arguments.SetCancellationToken(i, default);
                continue;
            }

            var item = baseSerializer.Read(ref data, type);
            arguments.SetUntyped(i, item);
        }
    }

    // Private methods

    private ReadOnlyMemory<byte> PolySerialize(ArgumentList arguments, ArrayPoolBuffer<byte> buffer)
    {
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken))
                continue;

            var item = arguments.GetUntyped(i);
            if (IsPolymorphic(type)) {
                var itemType = item?.GetType() ?? type;
                ByteTypeSerializer.WriteDerivedItemType(buffer, type, itemType);
                baseSerializer.Write(buffer, item, itemType);
            }
            else
                baseSerializer.Write(buffer, item, type);
        }
        return GetWriteBufferMemory(buffer);
    }

    private void PolyDeserialize(ArgumentList arguments, ReadOnlyMemory<byte> data)
    {
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken)) {
                arguments.SetCancellationToken(i, default);
                continue;
            }

            object? item;
            if (IsPolymorphic(type)) {
                var itemType = ByteTypeSerializer.ReadDerivedItemType(ref data, type);
                item = baseSerializer.Read(ref data, itemType);
            }
            else
                item = baseSerializer.Read(ref data, type);
            arguments.SetUntyped(i, item);
        }
    }
}
