using ActualLab.Collections.Fixed;
using ActualLab.OS;

namespace ActualLab.Interception;

public sealed class ArgumentListType
{
    private static readonly ConcurrentDictionary<FixedArray3<Type?>, ArgumentListType> GDefCache3
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private static readonly ConcurrentDictionary<FixedArray3<Type?>, ArgumentListType> SDefCache3
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private static readonly ConcurrentDictionary<FixedArray6<Type?>, ArgumentListType> GDefCache6
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private static readonly ConcurrentDictionary<FixedArray6<Type?>, ArgumentListType> SDefCache6
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private static readonly ConcurrentDictionary<FixedArray10<Type?>, ArgumentListType> GDefCacheN
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private static readonly ConcurrentDictionary<FixedArray10<Type?>, ArgumentListType> SDefCacheN
        = new(HardwareInfo.ProcessorCountPo2, 128);
    private string? _toString;

    public readonly Type ListType;
    public readonly Type[] ItemTypes;
    public readonly Type[] GenericItemTypes;
    public readonly int ItemCount;
    public readonly int GenericItemCount;
    public readonly int SimpleItemCount;
    public readonly object?[] DefaultValues;
    public readonly Func<ArgumentList> Factory;

    public static ArgumentListType Get(params ReadOnlySpan<Type> itemTypes)
    {
        if (itemTypes.Length <= 3) // Primary scenario
            return ArgumentList.AllowGenerics || itemTypes.Length == 0
                ? GDefCache3.GetOrAdd(FixedArray3<Type?>.New(itemTypes!), static key => new ArgumentListType(true, key.ReadOnlySpan))
                : SDefCache3.GetOrAdd(FixedArray3<Type?>.New(itemTypes!), static key => new ArgumentListType(false, key.ReadOnlySpan));

        return itemTypes.Length <= 6
            ? Get6(ArgumentList.AllowGenerics, itemTypes)
            : GetN(ArgumentList.AllowGenerics, itemTypes);
    }

    public static ArgumentListType Get(bool useGenerics, params ReadOnlySpan<Type> itemTypes)
    {
        if (itemTypes.Length <= 3) // Primary scenario
            return useGenerics || itemTypes.Length == 0
                ? GDefCache3.GetOrAdd(FixedArray3<Type?>.New(itemTypes!), static key => new ArgumentListType(true, key.ReadOnlySpan))
                : SDefCache3.GetOrAdd(FixedArray3<Type?>.New(itemTypes!), static key => new ArgumentListType(false, key.ReadOnlySpan));

        return itemTypes.Length <= 6
            ? Get6(useGenerics, itemTypes)
            : GetN(useGenerics, itemTypes);
    }

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentListType Get6(bool useGenerics, in ReadOnlySpan<Type> itemTypes)
        => useGenerics
            ? GDefCache6.GetOrAdd(FixedArray6<Type?>.New(itemTypes!), static key => new ArgumentListType(true, key.ReadOnlySpan))
            : SDefCache6.GetOrAdd(FixedArray6<Type?>.New(itemTypes!), static key => new ArgumentListType(false, key.ReadOnlySpan));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentListType GetN(bool useGenerics, in ReadOnlySpan<Type> itemTypes)
        => useGenerics
            ? GDefCacheN.GetOrAdd(FixedArray10<Type?>.New(itemTypes!), static key => new ArgumentListType(true, key.ReadOnlySpan))
            : SDefCacheN.GetOrAdd(FixedArray10<Type?>.New(itemTypes!), static key => new ArgumentListType(false, key.ReadOnlySpan));

    private ArgumentListType(bool useGenerics, ReadOnlySpan<Type?> key)
    {
        Type[] itemTypes = key.ToArray().TakeWhile(x => x != null).ToArray()!;
        ItemTypes = itemTypes;
        ItemCount = itemTypes.Length;
        if (useGenerics) {
            GenericItemCount = Math.Min(ItemCount, ArgumentList.MaxGenericItemCount);
            GenericItemTypes = GenericItemCount == ItemCount
                ? ItemTypes
                : itemTypes.AsSpan(0, GenericItemCount).ToArray();
            ListType = ArgumentList.GenericTypes[itemTypes.Length];
            if (ListType.IsGenericType)
                ListType = ListType.MakeGenericType(GenericItemTypes);
        }
        else {
            if (itemTypes.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(useGenerics));

            GenericItemCount = 0;
            // ReSharper disable once UseCollectionExpression
            GenericItemTypes = Array.Empty<Type>();
            ListType = ArgumentList.SimpleTypes[itemTypes.Length];
        }

        SimpleItemCount = ItemCount - GenericItemCount;
        DefaultValues = new object?[ItemCount];
        for (var i = 0; i < ItemCount; i++)
            DefaultValues[i] = ItemTypes[i].GetDefaultValue();
        if (SimpleItemCount == 0)
            Factory = (Func<ArgumentList>)ListType.GetConstructorDelegate()!;
        else {
            var factory = (Func<ArgumentListType, ArgumentList>)ListType.GetConstructorDelegate(typeof(ArgumentListType))!;
            Factory = () => factory.Invoke(this);
        }
    }

    public override string ToString()
    {
        if (_toString != null)
            return _toString;

        var s = $"[<{ItemTypes.Select(x => x.GetName()).ToDelimitedString()}>]";
        var suffix = SimpleItemCount == 0 ? "g"
            : GenericItemCount == 0 ? "s"
            : $"g{GenericItemCount}s{SimpleItemCount}";
        _toString = s + suffix;
        return _toString;
    }

    public object? CastItem(int index, object? value)
    {
        var expectedType = ItemTypes[index];
        if (expectedType.IsInstanceOfType(value))
            return value;
        if (ReferenceEquals(value, null) && !expectedType.IsValueType)
            return value;

        return DefaultValues[index];
    }
}
