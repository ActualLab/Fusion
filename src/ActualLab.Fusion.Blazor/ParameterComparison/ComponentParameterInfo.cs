using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public sealed class ComponentParameterInfo
{
    public PropertyInfo Property { get; init; } = null!;
    public bool IsCascading { get; init; }
    public bool IsCapturingUnmatchedValues { get; init; }
    public string? CascadingParameterName { get; init; }
    public ParameterComparer Comparer { get; init; } = null!;
#pragma warning disable IL2026
    [field: AllowNull, MaybeNull]
    public Func<IComponent, object> Getter => field ??= Property.GetGetter<IComponent, object>(true);
    [field: AllowNull, MaybeNull]
    public Action<IComponent, object> Setter => field ??= Property.GetSetter<IComponent, object>(true);
#pragma warning restore IL2026
}
