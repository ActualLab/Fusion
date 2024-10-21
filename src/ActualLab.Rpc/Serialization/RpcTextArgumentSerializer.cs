using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Serialization.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcTextArgumentSerializer : RpcArgumentSerializer
{
    private static readonly byte Delimiter = 0x1e; // Record separator in ASCII / UTF8

    [ThreadStatic] private static Utf8TextWriter? _utf8Buffer;
    public static int Utf8BufferReplaceCapacity { get; set; } = 65536;

    private readonly ITextSerializer _serializer;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcTextArgumentSerializer(ITextSerializer serializer)
        => _serializer = serializer;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool allowPolymorphism, int sizeHint)
    {
        if (arguments.Length == 0)
            return default;

        var writer = _utf8Buffer ??= new Utf8TextWriter();
        try {
            var itemSerializer = allowPolymorphism
                ? (ItemSerializer)new ItemPolymorphicSerializer(_serializer, writer)
                : new ItemNonPolymorphicSerializer(_serializer, writer);
            arguments.Read(itemSerializer);
            var span = writer.Buffer.AsSpan();
            if (span.Length >= 1 && span[^1] == Delimiter)
                span = span[..^1];
            return span.ToArray();
        }
        finally {
            ref var buffer = ref writer.Buffer;
            if (buffer.Length <= Utf8BufferReplaceCapacity)
                buffer.Clear();
            else {
                buffer.Dispose();
                buffer = new Utf8ValueStringBuilder();
            }
        }
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

    private abstract class ItemSerializer(ITextSerializer serializer, Utf8TextWriter writer)
        : ArgumentListReader
    {
        protected readonly ITextSerializer Serializer = serializer;
        protected readonly Utf8TextWriter Writer = writer;

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnStruct<T>(T item, int index)
        {
            if (typeof(T) != typeof(CancellationToken)) {
                Serializer.Write(Writer, item, typeof(T));
                Writer.WriteLiteral(Delimiter);
            }
        }
    }

    private sealed class ItemPolymorphicSerializer(ITextSerializer serializer, Utf8TextWriter writer)
        : ItemSerializer(serializer, writer)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
        {
            var itemType = item?.GetType() ?? RequireNonAbstract(type);
            TextTypeSerializer.WriteDerivedItemType(Writer, type, itemType);
            Serializer.Write(Writer, item, itemType);
            Writer.WriteLiteral(Delimiter);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken)) {
                    Serializer.Write(Writer, item, type);
                    Writer.WriteLiteral(Delimiter);
                }
                return;
            }

            var itemType = item?.GetType() ?? RequireNonAbstract(type);
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
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
        {
            Writer.WriteLiteral(TextTypeSerializer.NullTypeSpan);
            Serializer.Write(Writer, item, RequireNonAbstract(type));
            Writer.WriteLiteral(Delimiter);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnAny(Type type, object? item, int index)
        {
            if (type.IsValueType) {
                if (type != typeof(CancellationToken)) {
                    Serializer.Write(Writer, item, type);
                    Writer.WriteLiteral(Delimiter);
                }
                return;
            }

            Writer.WriteLiteral(TextTypeSerializer.NullTypeSpan);
            Serializer.Write(Writer, item, RequireNonAbstract(type));
            Writer.WriteLiteral(Delimiter);
        }
    }

    // ItemDeserializer + its variants

    private abstract class ItemDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ArgumentListWriter
    {
        protected readonly ITextSerializer Serializer = serializer;
        protected ReadOnlyMemory<byte> Data = data;

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override T OnStruct<T>(int index)
            => typeof(T) == typeof(CancellationToken)
                ? default!
                : (T)Serializer.ReadDelimited(ref Data, typeof(T), Delimiter)!;
    }

    private sealed class ItemPolymorphicDeserializer(ITextSerializer serializer, ReadOnlyMemory<byte> data)
        : ItemDeserializer(serializer, data)
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnClass(Type type, int index)
        {
            var itemType = TextTypeSerializer.ReadDerivedItemType(ref Data, type);
            return Serializer.ReadDelimited(ref Data, itemType, Delimiter);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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
}
