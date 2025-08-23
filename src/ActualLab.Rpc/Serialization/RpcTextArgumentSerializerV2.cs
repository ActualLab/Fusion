using ActualLab.Interception;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Serialization.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcTextArgumentSerializerV1(ITextSerializer baseSerializer, bool forcePolymorphism = false)
    : RpcArgumentSerializer(forcePolymorphism)
{
    // We use US (Unit separator, 0x1F) character here.
    // RS (Record separator, 0x1E) is used by WebSocketChannel to compose N-message frames.
    private static readonly byte Delimiter = 0x1F;

    [ThreadStatic] private static Utf8TextWriter? _utf8Buffer;
    public static int Utf8BufferReplaceCapacity { get; set; } = 65536;

    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint)
    {
        var writer = _utf8Buffer ??= new Utf8TextWriter();
        try {
            var itemSerializer = (ItemSerializer)(ForcePolymorphism
                ? needsPolymorphism
                    ? new ItemPolymorphicSerializer(baseSerializer, writer)
                    : new ItemNonPolymorphicSerializer(baseSerializer, writer)
                : new ItemValueOnlySerializer(baseSerializer, writer));
            arguments.Read(itemSerializer);
            var span = writer.Buffer.AsSpan();
            if (span.Length >= 1 && span[^1] == Delimiter)
                span = span[..^1];
            return span.ToArray();
        }
        finally {
            writer.Renew(Utf8BufferReplaceCapacity);
        }
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

    private abstract class ItemSerializer(ITextSerializer serializer, Utf8TextWriter writer)
        : ArgumentListReader
    {
        protected readonly ITextSerializer Serializer = serializer;
        protected readonly Utf8TextWriter Writer = writer;

        public override void OnStruct<T>(T item, int index)
        {
            var type = typeof(T);
            if (type == typeof(CancellationToken))
                return;

            Serializer.Write(Writer, item, type);
            Writer.WriteLiteral(Delimiter);
        }
    }

    private sealed class ItemPolymorphicSerializer(ITextSerializer serializer, Utf8TextWriter writer)
        : ItemSerializer(serializer, writer)
    {
        public override void OnClass(Type type, object? item, int index)
        {
            var itemType = item?.GetType() ?? type;
            TextTypeSerializer.WriteDerivedItemType(Writer, type, itemType);
            Serializer.Write(Writer, item, itemType);
            Writer.WriteLiteral(Delimiter);
        }

        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type == typeof(CancellationToken))
                    return;

                Serializer.Write(Writer, item, type);
                Writer.WriteLiteral(Delimiter);
                return;
            }

            var itemType = item?.GetType() ?? type;
            TextTypeSerializer.WriteDerivedItemType(Writer, type, itemType);
            Serializer.Write(Writer, item, itemType);
            Writer.WriteLiteral(Delimiter);
        }
    }

    private sealed class ItemNonPolymorphicSerializer(
        ITextSerializer serializer,
        Utf8TextWriter writer)
        : ItemSerializer(serializer, writer)
    {
        public override void OnClass(Type type, object? item, int index)
        {
            Writer.WriteLiteral(TextTypeSerializer.NullTypeSpan);
            Serializer.Write(Writer, item, type);
            Writer.WriteLiteral(Delimiter);
        }

        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type == typeof(CancellationToken))
                    return;

                Serializer.Write(Writer, item, type);
                Writer.WriteLiteral(Delimiter);
                return;
            }

            Writer.WriteLiteral(TextTypeSerializer.NullTypeSpan);
            Serializer.Write(Writer, item, type);
            Writer.WriteLiteral(Delimiter);
        }
    }

    private sealed class ItemValueOnlySerializer(
        ITextSerializer serializer,
        Utf8TextWriter writer)
        : ItemSerializer(serializer, writer)
    {
        public override void OnClass(Type type, object? item, int index)
        {
            Serializer.Write(Writer, item, type);
            Writer.WriteLiteral(Delimiter);
        }

        public override void OnAny(Type type, object? item, int index)
        {
            if (type == typeof(CancellationToken))
                return;

            Serializer.Write(Writer, item, type);
            Writer.WriteLiteral(Delimiter);
        }
    }

    // ItemDeserializer + its variants

    private abstract class ItemDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ArgumentListWriter
    {
        protected readonly ITextSerializer Serializer = serializer;
        protected ReadOnlyMemory<byte> Data = data;

        public override T OnStruct<T>(int index)
        {
            var type = typeof(T);
            return type == typeof(CancellationToken)
                ? default!
                : (T)Serializer.ReadDelimited(ref Data, type, Delimiter)!;
        }
    }

    private sealed class ItemPolymorphicDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
        {
            var itemType = TextTypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.ReadDelimited(ref Data, itemType, Delimiter);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.ReadDelimited(ref Data, type, Delimiter);

            var itemType = TextTypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.ReadDelimited(ref Data, itemType, Delimiter);
        }
    }

    private sealed class ItemNonPolymorphicDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
        {
            TextTypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.ReadDelimited(ref Data, type, Delimiter);
        }

        public override object? OnAny(Type type, int index, object? defaultValue)
        {
            if (type.IsValueType)
                return type == typeof(CancellationToken)
                    ? defaultValue
                    : Serializer.ReadDelimited(ref Data, type, Delimiter);

            TextTypeSerializer.ReadExactItemType(ref Data, type);
            return Serializer.ReadDelimited(ref Data, type, Delimiter);
        }
    }

    private sealed class ItemValueOnlyDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        public override object? OnClass(Type type, int index)
            => Serializer.ReadDelimited(ref Data, type, Delimiter);

        public override object? OnAny(Type type, int index, object? defaultValue)
            => type == typeof(CancellationToken)
                ? defaultValue
                : Serializer.ReadDelimited(ref Data, type, Delimiter);
    }
}
