using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Base Blazor component with custom parameter comparison and event handling
/// to reduce unnecessary re-renders.
/// </summary>
public abstract class FusionComponentBase : ComponentBase, IHandleEvent
{
    public static ParameterComparisonMode DefaultParameterComparisonMode { get; set; } = ParameterComparisonMode.Custom;

    protected bool MustRenderAfterEvent { get; set; } = true;
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume Blazor components' code is fully preserved")]
    protected ComponentInfo ComponentInfo => field ??= ComponentInfo.Get(GetType());
    protected int ParameterSetIndex { get; set; }
    protected internal Action StateHasChangedInvoker => field ??= StateHasChanged;
    private ContextCallback DispatchStateHasChangedInvoker
        => field ??= state => _ = ((Dispatcher)state!).InvokeAsync(StateHasChangedInvoker);


    public override Task SetParametersAsync(ParameterView parameters)
    {
        var parameterSetIndex = ParameterSetIndex;
        if (parameterSetIndex != 0 && !ComponentInfo.ShouldSetParameters(this, parameters))
            return Task.CompletedTask;

        ParameterSetIndex = ++parameterSetIndex;
        return base.SetParametersAsync(parameters);
    }

    public void NotifyStateHasChanged(bool isolate = false)
    {
        try {
            var dispatcher = this.GetDispatcher();
            if (dispatcher.CheckAccess()) {
                StateHasChanged(); // Renders in-place, so there is no ExecutionContext to isolate
                return;
            }

            if (isolate || (SafeDispatcher.IsUnsafe(dispatcher) && ExecutionContext.IsFlowSuppressed()))
                ExecutionContext.Run(ExecutionContextExt.Default, DispatchStateHasChangedInvoker, dispatcher);
            else
                _ = dispatcher.InvokeAsync(StateHasChangedInvoker);
        }
        catch (ObjectDisposedException) {
            // Intended
        }
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
                await task1.ConfigureAwait(true);
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
