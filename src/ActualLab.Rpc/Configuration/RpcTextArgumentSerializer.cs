using ActualLab.Interception;

namespace ActualLab.Rpc;

public sealed class RpcTextArgumentSerializer(ITextSerializer serializer) : RpcArgumentSerializer
{
    public override TextOrBytes Serialize(ArgumentList arguments, bool allowPolymorphism)
    {
        if (arguments.Length == 0)
            return TextOrBytes.EmptyText;

        var collector = new List<string>();

        var itemSerializer = allowPolymorphism
            ? (ItemSerializer)new ItemPolymorphicSerializer(serializer, collector)
            : new ItemNonPolymorphicSerializer(serializer, collector);

        arguments.Read(itemSerializer);

        using var f = ListFormat.Default.CreateFormatter();

        foreach (var item in collector) {
            f.Append(item);
        }

        f.AppendEnd();

        return new TextOrBytes(f.Output);
    }

    public override void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, TextOrBytes data)
    {
        if (!data.IsText(out var text))
            throw new ArgumentOutOfRangeException(nameof(data));

        if (text.IsEmpty)
            return;

        var items = ListFormat.Default.CreateParser(text.Span).ParseAll();

        var deserializer = new ItemDeserializer(serializer, items.ToArray());

        arguments.Write(deserializer);
    }

    // Nested types

    private abstract class ItemSerializer(ITextSerializer serializer, List<string> collector) : ArgumentListReader
    {
        protected readonly ITextSerializer Serializer = serializer;
        protected readonly List<string> Collector = collector;

        public override void OnStruct<T>(T item, int index)
        {
            if (typeof(T) != typeof(CancellationToken)) {
                Collector.Add(Serializer.Write(item));
            }
        }
    }

    private sealed class ItemPolymorphicSerializer(ITextSerializer serializer, List<string> collector)
        : ItemSerializer(serializer, collector)
    {
        private readonly ITextSerializer _polymorphicSerializer = new TypeDecoratingTextSerializer(serializer);

        public override void OnObject(Type type, object? item, int index)
            => Collector.Add(_polymorphicSerializer.Write(item, type));
    }

    private sealed class ItemNonPolymorphicSerializer(ITextSerializer serializer, List<string> collector)
        : ItemSerializer(serializer, collector)
    {
        public override void OnObject(Type type, object? item, int index)
            => Collector.Add(Serializer.Write(item, type));
    }

    private sealed class ItemDeserializer(ITextSerializer serializer, string[] data) : ArgumentListWriter
    {
        private readonly ITextSerializer _polymorphicSerializer = new TypeDecoratingTextSerializer(serializer);

        public override T OnStruct<T>(int index)
        {
            if (typeof(T) == typeof(CancellationToken))
                return default!;

            return serializer.Read<T>(data[index]);
        }

        public override object? OnObject(Type type, int index)
        {
            var itemData = data[index];

            var hasPolymorphValue = itemData.Split(ListFormat.Default.Delimiter).Length > 1;

            var actualSerializer = hasPolymorphValue ? _polymorphicSerializer : serializer;

            return actualSerializer.Read(itemData, type);
        }
    }
}
