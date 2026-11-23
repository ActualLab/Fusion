using MemoryPack.Formatters;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

// MemoryPack's collection formatters constrain the collection to ICollection<T?> rather than ICollection<T>
#pragma warning disable CS8631

/// <summary>
/// MemoryPack formatter for <see cref="ApiArray{T}"/>.
/// </summary>
public sealed class ApiArrayMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<ApiArray<T>>
{
    protected override int MemberCount => 1;

    public ApiArrayMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ApiArray<T> value)
    {
        writer.WriteArray(value.Items);
        offsets[0] = writer.WrittenCount;
    }

    protected override ApiArray<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
        => new(deltas[0] != 0 ? (T[])reader.ReadArray<T>()! : null!);
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiList{T}"/> - a plain collection,
/// like the one <c>GenerateType.Collection</c> registers.
/// </summary>
public sealed class ApiListMemoryPackFormatter<T> : MemoryPackFormatter<ApiList<T>>
{
    private static readonly GenericCollectionFormatter<ApiList<T>, T> Formatter = new();

    public ApiListMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref ApiList<T>? value)
        => Formatter.Serialize(ref writer, ref value);

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ApiList<T>? value)
        => Formatter.Deserialize(ref reader, ref value);
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiSet{T}"/> - a plain set,
/// like the one <c>GenerateType.Collection</c> registers.
/// </summary>
public sealed class ApiSetMemoryPackFormatter<T> : GenericSetFormatterBase<ApiSet<T>, T>
{
    public ApiSetMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override ApiSet<T> CreateSet()
        => new();
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiMap{TKey,TValue}"/> - a plain dictionary,
/// like the one <c>GenerateType.Collection</c> registers.
/// </summary>
public sealed class ApiMapMemoryPackFormatter<TKey, TValue>
    : GenericDictionaryFormatterBase<ApiMap<TKey, TValue>, TKey, TValue>
    where TKey : notnull
{
    public ApiMapMemoryPackFormatter()
    {
        MemoryPackSerialization.Register<TKey>();
        MemoryPackSerialization.Register<TValue>();
    }

    protected override ApiMap<TKey, TValue> CreateDictionary()
        => new();
}
