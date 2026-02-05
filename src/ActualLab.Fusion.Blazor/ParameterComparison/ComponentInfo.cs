using System.Collections.ObjectModel;
using ActualLab.OS;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Cached metadata about a Blazor component type, including its parameters
/// and their associated comparers for custom change detection.
/// </summary>
public sealed class ComponentInfo
{
    private static readonly ConcurrentDictionary<Type, LazySlim<Type, ComponentInfo>> ComponentInfoCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ILogger? DebugLog { get; set; }

    public Type Type { get; }
    public bool HasCustomParameterComparers { get; }
    public ParameterComparisonMode ParameterComparisonMode { get; }
    public IReadOnlyDictionary<string, ComponentParameterInfo> Parameters { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume Blazor components' code is fully preserved")]
    public static ComponentInfo Get(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType)
        => ComponentInfoCache.GetOrAdd(componentType, static t => new ComponentInfo(t));

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Blazor components' code is fully preserved")]
    private ComponentInfo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (!typeof(IComponent).IsAssignableFrom(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        DebugLog?.LogDebug("[+] ComponentInfo({Type})", type.GetName());

        ComponentInfo? parentComponentInfo = null;
        if (typeof(IComponent).IsAssignableFrom(type.BaseType))
            parentComponentInfo = Get(type.BaseType!);

        var parameterComparerProvider = ParameterComparerProvider.Instance;
        var parameters = new Dictionary<string, ComponentParameterInfo>(StringComparer.Ordinal);
        if (parentComponentInfo is not null)
            parameters.AddRange(parentComponentInfo.Parameters);

        var hasCustomParameterComparers = parentComponentInfo?.HasCustomParameterComparers ?? false;
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
        foreach (var property in type.GetProperties(bindingFlags)) {
            var pa = property.GetCustomAttribute<ParameterAttribute>(true);
            CascadingParameterAttribute? cpa = null;
            if (pa is null) {
                cpa = property.GetCustomAttribute<CascadingParameterAttribute>(true);
                if (cpa is null)
                    continue; // Not a parameter
            }

            var comparer = parameterComparerProvider.Get(property);
            hasCustomParameterComparers |= comparer is not DefaultParameterComparer;
            var parameter = new ComponentParameterInfo() {
                Property = property,
                IsCascading = cpa is not null,
                IsCapturingUnmatchedValues = pa?.CaptureUnmatchedValues ?? false,
                CascadingParameterName = cpa?.Name,
                Comparer = comparer,
            };
            parameters.Add(parameter.Property.Name, parameter);
        }
        Type = type;
        Parameters = new ReadOnlyDictionary<string, ComponentParameterInfo>(parameters);
        HasCustomParameterComparers = hasCustomParameterComparers;

        var fca = type.GetCustomAttribute<FusionComponentAttribute>(false);
        ParameterComparisonMode = fca?.ParameterComparisonMode.NullIfInherited()
            ?? parentComponentInfo?.ParameterComparisonMode
            ?? CircuitHubComponentBase.DefaultParameterComparisonMode;
    }

    public bool ShouldSetParameters(ComponentBase component, ParameterView parameterView)
    {
        if (!HasCustomParameterComparers || ParameterComparisonMode == ParameterComparisonMode.Standard)
            return true; // No custom comparers -> trigger the default flow

        var parameters = Parameters;
        foreach (var parameterValue in parameterView) {
            if (!parameters.TryGetValue(parameterValue.Name, out var parameterInfo))
                return true; // Unknown parameter -> trigger default flow

            var oldValue = parameterInfo.Getter.Invoke(component);
            if (!parameterInfo.Comparer.AreEqual(oldValue, parameterValue.Value))
                return true; // Comparer says values aren't equal -> trigger default flow
        }

        return false; // All parameter values are equal
    }
}
