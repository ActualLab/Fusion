using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public class FusionComponentBase : ComponentBase
{
    public static ParameterComparisonMode DefaultParameterComparisonMode { get; set; } = ParameterComparisonMode.Custom;

    private ComponentInfo? _componentInfo;
    private Action? _stateHasChangedInvoker;

    protected ComponentInfo ComponentInfo => _componentInfo ??= ComponentInfo.Get(GetType());
    protected bool IsFirstSetParametersCallCompleted { get; private set; }

    internal Action StateHasChangedInvoker => _stateHasChangedInvoker ??= StateHasChanged;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (!IsFirstSetParametersCallCompleted) {
            var result = base.SetParametersAsync(parameters);
            IsFirstSetParametersCallCompleted = true;
            return result;
        }
        return ComponentInfo.ShouldSetParameters(this, parameters)
            ? base.SetParametersAsync(parameters)
            : Task.CompletedTask;
    }
}
