using System.Buffers;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteArgumentSerializerV2(IByteSerializer baseSerializer, bool forcePolymorphism = false)
    : RpcArgumentSerializer(forcePolymorphism)
{
    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint)
    {
        var buffer = GetWriteBuffer(sizeHint);
        var itemSerializer = (ItemSerializer)(ForcePolymorphism
            ? needsPolymorphism
                ? new ItemPolymorphicSerializer(baseSerializer, buffer)
                : new ItemNonPolymorphicSerializer(baseSerializer, buffer)
            : new ItemValueOnlySerializer(baseSerializer, buffer));
        arguments.Read(itemSerializer);
        return GetWriteBufferMemory(buffer);
    }

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
    {
        var itemDeserializer = (ItemDeserializer)(ForcePolymorphism
            ? needsPolymorphism
                ? new ItemPolymorphicDeserializer(baseSerializer, data)
                : new ItemNonPolymorphicDeserializer(baseSerializer, data)
            : new ItemValueOnlyDeserializer(baseSerializer, data));
        arguments.Write(itemDeserializer);
    }

    // ItemSerializer + its variants

    private abstract class ItemSerializer(IByteSerializer serializer, IBufferWriter<byte> buffer)
        : ArgumentListReader
    {
        protected readonly IByteSerializer Serializer = serializer;
        protected readonly IBufferWriter<byte> Buffer = buffer;

        public override void OnStruct<T>(T item, int index)
        {
            var type = typeof(T);
            if (type != typeof(CancellationToken))
                Serializer.Write(Buffer, item, type);
        }
    }

    private sealed class ItemPolymorphicSerializer(IByteSerializer serializer, IBufferWriter<byte> buffer)
        : ItemSerializer(serializer, buffer)
    {
        public override void OnClass(Type type, object? item, int index)
        {
            var itemType = item?.GetType() ?? type;
            ByteTypeSerializer.WriteDerivedItemType(Buffer, type, itemType);
            Serializer.Write(Buffer, item, itemType);
        }

        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken))
                    Serializer.Write(Buffer, item, type);
                return;
            }

            var itemType = item?.GetType() ?? type;
            ByteTypeSerializer.WriteDerivedItemType(Buffer, type, itemType);
            Serializer.Write(Buffer, item, itemType);
        }
    }

    private sealed class ItemNonPolymorphicSerializer(
        IByteSerializer serializer,
        IBufferWriter<byte> buffer)
        : ItemSerializer(serializer, buffer)
    {
        public override void OnClass(Type type, object? item, int index)
        {
            Buffer.Append(ByteTypeSerializer.NullTypeSpan);
            Serializer.Write(Buffer, item, type);
        }

        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken))
                    Serializer.Write(Buffer, item, type);
                return;
            }

            Buffer.Append(ByteTypeSerializer.NullTypeSpan);
            Serializer.Write(Buffer, item, type);
        }
    }

    private sealed class ItemValueOnlySerializer(
        IByteSerializer serializer,
        IBufferWriter<byte> buffer)
        : ItemSerializer(serializer, buffer)
    {
        public override void OnClass(Type type, object? item, int index)
            => Serializer.Write(Buffer, item, type);

        public override void OnAny(Type type, object? item, int index)
        {
            if (type != typeof(CancellationToken))
                Serializer.Write(Buffer, item, type);
        }
    }

    // ItemDeserializer + its variants

    private abstract class ItemDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ArgumentListWriter
    {
        protected readonly IByteSerializer Serializer = serializer;
        protected ReadOnlyMemory<byte> Data = data;

        public override T OnStruct<T>(int index)
            => typeof(T) == typeof(CancellationToken)
                ? default!
                : Serializer.Read<T>(ref Data);
    }

    private sealed class ItemPolymorphicDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
        {
            var itemType = ByteTypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.Read(ref Data, itemType);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            var itemType = ByteTypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.Read(ref Data, itemType);
        }
    }

    private sealed class ItemNonPolymorphicDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
        {
            ByteTypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.Read(ref Data, type);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            ByteTypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.Read(ref Data, type);
        }
    }

    private sealed class ItemValueOnlyDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
            => Serializer.Read(ref Data, type);

        public override object? OnAny(Type type, int index, object? defaultValue)
            => type == typeof(CancellationToken)
                ? defaultValue
                : Serializer.Read(ref Data, type);
    }
}
