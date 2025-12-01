using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Blazor components' code is fully preserved")]
public sealed class ComponentParameterInfo
{
    public PropertyInfo Property { get; init; } = null!;
    public bool IsCascading { get; init; }
    public bool IsCapturingUnmatchedValues { get; init; }
    public string? CascadingParameterName { get; init; }
    public ParameterComparer Comparer { get; init; } = null!;
    public Func<IComponent, object> Getter => field ??= Property.GetGetter<IComponent, object>(true);
    public Action<IComponent, object> Setter => field ??= Property.GetSetter<IComponent, object>(true);
}
