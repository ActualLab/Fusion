using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

#pragma warning disable CA2007

public class FusionComponentBase : ComponentBase, IHandleEvent
{
    public static ParameterComparisonMode DefaultParameterComparisonMode { get; set; } = ParameterComparisonMode.Custom;

    private ComponentInfo? _componentInfo;
    private Action? _stateHasChangedInvoker;

    protected bool MustRenderAfterEvent { get; set; } = true;
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

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        // This code provides support for EnableStateHasChangedCallAfterEvent option
        // See https://github.com/dotnet/aspnetcore/issues/18919#issuecomment-803005864
        var task = callback.InvokeAsync(arg);
        if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
            return CompleteAsync(task);

        if (MustRenderAfterEvent)
            StateHasChanged();
        return Task.CompletedTask;

        async Task CompleteAsync(Task task1) {
            try {
                await task1;
            }
            catch {
                // Avoiding exception filters for AOT runtime support.
                // Ignore cancellations, but don't bother issuing a state change.
                if (task1.IsCanceled)
                    return;
                throw;
            }
            if (MustRenderAfterEvent)
                StateHasChanged();
        }
    }
}
