using ActualLab.Internal;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Resolves <see cref="ParameterComparer"/> instances for Blazor component parameters
/// based on attributes and known immutable types.
/// </summary>
public class ParameterComparerProvider
{
    private static readonly ConcurrentDictionary<Type, LazySlim<Type, ParameterComparer>> Cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ParameterComparerProvider Instance { get; set; } = new();

    public bool UseByValueParameterComparerForEnumProperties { get; init; } = true;
    public Dictionary<Type, Type> KnownComparerTypes { get; init; } = new() {
        { typeof(Symbol), typeof(ByValueParameterComparer) },
        { typeof(TimeSpan), typeof(ByValueParameterComparer) },
        { typeof(Moment), typeof(ByValueParameterComparer) },
        { typeof(DateTimeOffset), typeof(ByValueParameterComparer) },
#if NET6_0_OR_GREATER
        { typeof(DateOnly), typeof(ByValueParameterComparer) },
        { typeof(TimeOnly), typeof(ByValueParameterComparer) },
#endif
    };

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "False positive")]
    public static ParameterComparer Get(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type? comparerType)
    {
        if (comparerType is null)
            return DefaultParameterComparer.Instance;

        return Cache.GetOrAdd(comparerType, static comparerType1 => {
            if (!typeof(ParameterComparer).IsAssignableFrom(comparerType1))
                throw new ArgumentOutOfRangeException(nameof(comparerType));
            return (ParameterComparer)comparerType1.CreateInstance();
        });
    }

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public virtual ParameterComparer Get(PropertyInfo property)
    {
        var comparerType = GetComparerType(property);
        return Get(comparerType);
    }

    public virtual Type? GetComparerType(PropertyInfo property)
    {
        var type = property.GetCustomAttribute<ParameterComparerAttribute>(true)?.ComparerType;
        if (type is not null)
            return type;

        type = GetKnownComparerType(property);
        if (type is not null)
            return type;

        type = property.PropertyType.GetCustomAttribute<ParameterComparerAttribute>(true)?.ComparerType;
        if (type is not null)
            return type;

        type = property.DeclaringType?.GetCustomAttribute<ParameterComparerAttribute>(true)?.ComparerType;
        if (type is not null)
            return type;

        return null;
    }

    protected virtual Type? GetKnownComparerType(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        if (UseByValueParameterComparerForEnumProperties && propertyType.IsEnum)
            return typeof(ByValueParameterComparer);

        return KnownComparerTypes.GetValueOrDefault(propertyType);
    }
}
