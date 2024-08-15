using ActualLab.Collections.Internal;

namespace ActualLab.Interception;

public sealed class ArgumentListType
{
    private static readonly ConcurrentDictionary<SequenceEqualityBox.ForArray<Type>, ArgumentListType> GenericDefCache = new();
    private static readonly ConcurrentDictionary<SequenceEqualityBox.ForArray<Type>, ArgumentListType> SimpleDefCache = new();
    private string? _toString;

    public readonly Type ListType;
    public readonly Type[] ItemTypes;
    public readonly Type[] GenericItemTypes;
    public readonly int ItemCount;
    public readonly int GenericItemCount;
    public readonly int SimpleItemCount;
    public readonly object?[] DefaultValues;
    public readonly Func<ArgumentList> Factory;

    public static ArgumentListType Get(params Type[] itemTypes)
        => Get(ArgumentList.AllowGenerics, itemTypes);
    public static ArgumentListType Get(bool useGenerics, params Type[] itemTypes)
        => useGenerics
            ? GenericDefCache.GetOrAdd(new(itemTypes),
                static key => new ArgumentListType(true, key.Source))
            : SimpleDefCache.GetOrAdd(new(itemTypes),
                static key => key.Source.Length == 0 ? Get(true) : new ArgumentListType(false, key.Source));

    private ArgumentListType(bool useGenerics, Type[] itemTypes)
    {
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
            GenericItemCount = 0;
            // ReSharper disable once UseCollectionExpression
            GenericItemTypes = Array.Empty<Type>();
            ListType = ArgumentList.SimpleTypes[itemTypes.Length];
        }

        SimpleItemCount = ItemCount - GenericItemCount;
        DefaultValues = new object?[ItemCount];
        for (var i = 0; i < ItemCount; i++) {
            var t = ItemTypes[i];
            if (t.IsValueType)
                DefaultValues[i] = RuntimeHelpers.GetUninitializedObject(t);
        }
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
