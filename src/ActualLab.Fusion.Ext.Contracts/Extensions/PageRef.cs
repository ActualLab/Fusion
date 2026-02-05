using System.Globalization;
using MessagePack;

namespace ActualLab.Fusion.Extensions;

/// <summary>
/// Base class for cursor-based pagination references.
/// </summary>
public abstract record PageRef : IHasToStringProducingJson
{
    public static PageRef<TKey> New<TKey>(int count)
        => new(count);
    public static PageRef<TKey> New<TKey>(int count, TKey after)
        => new(count, Option.Some(after));
    public static PageRef<TKey> New<TKey>(int count, Option<TKey> after)
        => new(count, after);

    public static PageRef<TKey> Parse<TKey>(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? new PageRef<TKey>(count)
            : SystemJsonSerializer.Default.Read<PageRef<TKey>>(value);
}

/// <summary>
/// A typed cursor-based pagination reference with a count and an optional "after" key.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public partial record PageRef<TKey>(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int Count,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] Option<TKey> After = default
    ) : PageRef
{
    public override string ToString()
        => After.IsNone
            ? Count.ToString(CultureInfo.InvariantCulture)
            : SystemJsonSerializer.Default.Write(this, GetType());

    public static implicit operator PageRef<TKey>(int count)
        => new(count);
    public static implicit operator PageRef<TKey>((int Count, TKey AfterKey) source)
        => new(source.Count, Option.Some(source.AfterKey));
    public static implicit operator PageRef<TKey>((int Count, Option<TKey> AfterKey) source)
        => new(source.Count, source.AfterKey);
}
