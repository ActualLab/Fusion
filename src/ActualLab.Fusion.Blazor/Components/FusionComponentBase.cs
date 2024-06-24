using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public class FusionComponentBase : ComponentBase
{
    public static ParameterComparisonMode DefaultParameterComparisonMode { get; set; } = ParameterComparisonMode.Custom;

    private ComponentInfo? _componentInfo;
    private Action? _stateHasChangedInvoker;

    protected ComponentInfo ComponentInfo => _componentInfo ??= ComponentInfo.Get(GetType());
    protected int ParameterSetIndex { get; set; }

    internal Action StateHasChangedInvoker => _stateHasChangedInvoker ??= StateHasChanged;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var parameterSetIndex = ParameterSetIndex;
        if (parameterSetIndex != 0 && !ComponentInfo.ShouldSetParameters(this, parameters))
            return Task.CompletedTask;

        ParameterSetIndex = ++parameterSetIndex;
        return base.SetParametersAsync(parameters);
    }
}
