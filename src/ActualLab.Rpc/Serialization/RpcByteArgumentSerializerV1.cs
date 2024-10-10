using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.IO;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteArgumentSerializerV1 : RpcArgumentSerializer
{
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _writeBuffer;
    public static int WriteBufferCapacity { get; set; } = 16384;
    public static int WriteBufferReplaceCapacity { get; set; } = 65536;

    private readonly IByteSerializer _serializer;
    private readonly byte[] _defaultTypeRefBytes;

    public RpcByteArgumentSerializerV1(IByteSerializer serializer)
    {
        _serializer = serializer;
        using var buffer = serializer.Write(default(TypeRef));
        _defaultTypeRefBytes = buffer.WrittenSpan.ToArray();
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool allowPolymorphism, int sizeHint)
    {
        if (arguments.Length == 0)
            return default;

        var buffer = GetWriteBuffer(sizeHint);
        var itemSerializer = allowPolymorphism
            ? (ItemSerializer)new ItemPolymorphicSerializer(_serializer, buffer)
            : new ItemNonPolymorphicSerializer(_serializer, buffer, _defaultTypeRefBytes);
        arguments.Read(itemSerializer);
        return GetWriteBufferMemory(buffer);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
            return;

        var deserializer = allowPolymorphism
            ? (ItemDeserializer)new ItemPolymorphicDeserializer(_serializer, data)
            : new ItemNonPolymorphicDeserializer(_serializer, data);
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
            var typeRef = itemType == type ? default : new TypeRef(itemType).WithoutAssemblyVersions();
            Serializer.Write(Buffer, typeRef);
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
            var typeRef = itemType == type ? default : new TypeRef(itemType).WithoutAssemblyVersions();
            Serializer.Write(Buffer, typeRef);
            Serializer.Write(Buffer, item, itemType);
        }
    }

    private sealed class ItemNonPolymorphicSerializer(IByteSerializer serializer, IBufferWriter<byte> buffer, byte[] defaultTypeRefBytes)
        : ItemSerializer(serializer, buffer)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
        {
            // The code below is a faster equivalent of:
            // Serializer.Write(Buffer, default(TypeRef));
            // Serializer.Write(Buffer, item, type);
            var bufferSpan = Buffer.GetSpan(defaultTypeRefBytes.Length);
            defaultTypeRefBytes.CopyTo(bufferSpan);
            Buffer.Advance(defaultTypeRefBytes.Length);
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

            // The code below is a faster equivalent of:
            // Serializer.Write(Buffer, default(TypeRef));
            // Serializer.Write(Buffer, item, type);
            var bufferSpan = Buffer.GetSpan(defaultTypeRefBytes.Length);
            defaultTypeRefBytes.CopyTo(bufferSpan);
            Buffer.Advance(defaultTypeRefBytes.Length);
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
            var typeRef = Serializer.Read<TypeRef>(ref Data);
            var itemType = typeRef == default ? type : typeRef.Resolve();
            if (itemType != type && !type.IsAssignableFrom(itemType))
                throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(type, itemType);

            return Serializer.Read(ref Data, itemType);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            var typeRef = Serializer.Read<TypeRef>(ref Data);
            var itemType = typeRef == default ? type : typeRef.Resolve();
            if (itemType != type && !type.IsAssignableFrom(itemType))
                throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(type, itemType);

            return Serializer.Read(ref Data, itemType);
        }
    }

    private sealed class ItemNonPolymorphicDeserializer(IByteSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnClass(Type type, int index)
        {
            var typeRef = Serializer.Read<TypeRef>(ref Data);
            if (typeRef != default && typeRef.Resolve() is var itemType && itemType != type)
                throw Errors.CannotDeserializeUnexpectedArgumentType(type, itemType);

            return Serializer.Read(ref Data, type);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.Read(ref Data, type);

            var typeRef = Serializer.Read<TypeRef>(ref Data);
            if (typeRef != default && typeRef.Resolve() is var itemType && itemType != type)
                throw Errors.CannotDeserializeUnexpectedArgumentType(type, itemType);

            return Serializer.Read(ref Data, type);
        }
    }
}
