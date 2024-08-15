using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcTextArgumentSerializer(ITextSerializer serializer) : RpcArgumentSerializer
{
    public ListFormat ListFormat { get; init; } = ListFormat.Default;

    private readonly ITextSerializer _polymorphicSerializer = new TypeDecoratingTextSerializer(serializer);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override TextOrBytes Serialize(ArgumentList arguments, bool allowPolymorphism)
    {
        if (arguments.Length == 0)
            return TextOrBytes.EmptyText;

        var items = new List<string>();
        var itemSerializer = new ItemSerializer(GetSerializer(allowPolymorphism), items);
        arguments.Read(itemSerializer);

        using var f = ListFormat.CreateFormatter();
        foreach (var item in items)
            f.Append(item);
        f.AppendEnd();
        return new TextOrBytes(f.Output);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, TextOrBytes data)
    {
        if (!data.IsText(out var text))
            throw new ArgumentOutOfRangeException(nameof(data));
        if (text.IsEmpty)
            return;

        var items = ListFormat.CreateParser(text.Span).ParseAll();
        var deserializer = new ItemDeserializer(GetSerializer(allowPolymorphism), items);
        arguments.Write(deserializer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ITextSerializer GetSerializer(bool allowPolymorphism)
        => allowPolymorphism ? _polymorphicSerializer : serializer;

    // Nested types

    private sealed class ItemSerializer(ITextSerializer serializer, List<string> items) : ArgumentListReader
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnStruct<T>(T item, int index)
        {
            if (typeof(T) != typeof(CancellationToken))
                items.Add(serializer.Write(item));
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnClass(Type type, object? item, int index)
            => items.Add(serializer.Write(item, type));

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override void OnAny(Type type, object? item, int index)
        {
            if (type != typeof(CancellationToken))
                items.Add(serializer.Write(item, type));
        }
    }

    private sealed class ItemDeserializer(ITextSerializer serializer, List<string> items) : ArgumentListWriter
    {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override T OnStruct<T>(int index)
            => typeof(T) == typeof(CancellationToken)
                ? default!
                : serializer.Read<T>(items[index]);

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnClass(Type type, int index)
            => serializer.Read(items[index], type);

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public override object? OnAny(Type type, int index, object? defaultValue)
            => type == typeof(CancellationToken)
                ? defaultValue
                : serializer.Read(items[index], type);
    }
}
