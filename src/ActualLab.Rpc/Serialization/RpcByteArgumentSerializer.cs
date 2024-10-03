using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteArgumentSerializer : RpcArgumentSerializer
{
    public static int CopySizeThreshold { get; set; } = 1024;

    private readonly IByteSerializer _serializer;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcByteArgumentSerializer(IByteSerializer serializer)
        => _serializer = serializer;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override TextOrBytes Serialize(ArgumentList arguments, bool allowPolymorphism, int sizeHint)
    {
        if (arguments.Length == 0)
            return TextOrBytes.EmptyBytes;

        var buffer = new ArrayPoolBuffer<byte>(128 + Math.Max(128, sizeHint));
        try {
            var itemSerializer = allowPolymorphism
                ? (ItemSerializer)new ItemPolymorphicSerializer(_serializer, buffer)
                : new ItemNonPolymorphicSerializer(_serializer, buffer);
            arguments.Read(itemSerializer);

            ReadOnlyMemory<byte> bytes;
            var position = buffer.Position;
            if (position < CopySizeThreshold || position < buffer.FreeCapacity) {
                bytes = buffer.WrittenSpan.ToArray();
                buffer.Dispose();
            }
            else
                bytes = buffer.WrittenMemory;
            return new TextOrBytes(bytes);
        }
        catch {
            buffer.Dispose();
            throw;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, TextOrBytes data)
    {
        if (!data.IsBytes(out var bytes))
            throw new ArgumentOutOfRangeException(nameof(data));
        if (bytes.IsEmpty)
            return;

        var deserializer = allowPolymorphism
            ? (ItemDeserializer)new ItemPolymorphicDeserializer(_serializer, bytes)
            : new ItemNonPolymorphicDeserializer(_serializer, bytes);
        arguments.Write(deserializer);
    }

    // ItemSerializer + its variants

    private abstract class ItemSerializer(IByteSerializer serializer, IBufferWriter<byte> buffer)
        : ArgumentListReader
    {
        protected readonly IByteSerializer Serializer = serializer;
        protected readonly IBufferWriter<byte> Buffer = buffer;

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnStruct<T>(T item, int index)
        {
            if (typeof(T) != typeof(CancellationToken))
                Serializer.Write(Buffer, item);
        }
    }

    private sealed class ItemPolymorphicSerializer(IByteSerializer serializer, IBufferWriter<byte> buffer)
        : ItemSerializer(serializer, buffer)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
        {
            var itemType = item?.GetType() ?? type;
            TypeSerializer.WriteDerivedItemType(Buffer, type, itemType);
            Serializer.Write(Buffer, item, itemType);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken))
                    Serializer.Write(Buffer, item, type);
                return;
            }

            var itemType = item?.GetType() ?? type;
            TypeSerializer.WriteDerivedItemType(Buffer, type, itemType);
            Serializer.Write(Buffer, item, itemType);
        }
    }

    private sealed class ItemNonPolymorphicSerializer(
        IByteSerializer serializer,
        IBufferWriter<byte> buffer)
        : ItemSerializer(serializer, buffer)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
        {
            Buffer.Append(TypeSerializer.NullTypeSpan);
            Serializer.Write(Buffer, item, type);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken))
                    Serializer.Write(Buffer, item, type);
                return;
            }

            Buffer.Append(TypeSerializer.NullTypeSpan);
            Serializer.Write(Buffer, item, type);
        }
    }

    // ItemDeserializer + its variants

    private abstract class ItemDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ArgumentListWriter
    {
        protected readonly IByteSerializer Serializer = serializer;
        protected ReadOnlyMemory<byte> Data = data;

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override T OnStruct<T>(int index)
            => typeof(T) == typeof(CancellationToken)
                ? default!
                : Serializer.Read<T>(ref Data);
    }

    private sealed class ItemPolymorphicDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnClass(Type type, int index)
        {
            var itemType = TypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.Read(ref Data, itemType);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            var itemType = TypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.Read(ref Data, itemType);
        }
    }

    private sealed class ItemNonPolymorphicDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnClass(Type type, int index)
        {
            TypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.Read(ref Data, type);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            TypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.Read(ref Data, type);
        }
    }
}
