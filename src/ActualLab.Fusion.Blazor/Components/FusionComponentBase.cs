using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

#pragma warning disable CA2007

public class FusionComponentBase : ComponentBase, IHandleEvent, IHasFusionHub
{
    public static ParameterComparisonMode DefaultParameterComparisonMode { get; set; } = ParameterComparisonMode.Custom;

    [Inject] protected FusionHub FusionHub { get; init; } = null!;

    // Most useful service shortcuts
    protected IServiceProvider Services => FusionHub.Services;
    protected Session Session => FusionHub.Session;
    protected StateFactory StateFactory => FusionHub.StateFactory;
    protected UICommander UICommander => FusionHub.UICommander;
    protected NavigationManager Nav => FusionHub.Nav;
    protected IJSRuntime JS => FusionHub.JS;

    // Explicit IHasFusionHub & IHasServices implementation
    FusionHub IHasFusionHub.FusionHub => FusionHub;
    IServiceProvider IHasServices.Services => Services;

    protected bool MustRenderAfterEvent { get; set; } = true;
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume Blazor components' code is fully preserved")]
    [field: AllowNull, MaybeNull]
    protected ComponentInfo ComponentInfo => field ??= ComponentInfo.Get(GetType());
    protected int ParameterSetIndex { get; set; }
    [field: AllowNull, MaybeNull]
    internal Action StateHasChangedInvoker => field ??= StateHasChanged;

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
                if (task1.IsCanceledOrFaultedWithOce())
                    return;
                throw;
            }
            if (MustRenderAfterEvent)
                StateHasChanged();
        }
    }
}
