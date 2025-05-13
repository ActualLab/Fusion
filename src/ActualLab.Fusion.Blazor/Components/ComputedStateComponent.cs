using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public abstract partial class ComputedStateComponent : StatefulComponentBase
{
    protected ComputedStateComponentOptions Options { get; set; } = DefaultOptions;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        // We completely re-implement SetParametersAsync flow here, coz
        // it's the only way to suppress some StateHasChanged() calls in base class.
        // It makes sense to do this in ComputedStateComponent, because its descendants
        // typically have to re-render only after their State gets (re)computed.

        var parameterSetIndex = ParameterSetIndex;
        var isInitializing = parameterSetIndex == 0;
        if (!(isInitializing || ComponentInfo.ShouldSetParameters(this, parameters)))
            return Task.CompletedTask; // State is already initialized in this case

        ParameterSetIndex = ++parameterSetIndex;
        parameters.SetParameterProperties(this);
        if (!isInitializing)
            return OnSetParametersFlow(false);

        ComponentExt.MarkInitialized(this);
        if (ReferenceEquals(State, null)) {
            var (state, stateOptions) = CreateState();
            SetState(state, stateOptions);
        }
        return OnInitializedFlow();
    }

    protected override bool ShouldRender()
    {
        var computed = State.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.RenderInconsistentState) != 0;
    }

    // Private methods

    private Task OnInitializedFlow()
    {
        OnInitialized();
        var whenInitialized = OnInitializedAsync();

        // Sync-async branching to speed up the "happy" sync path
        return whenInitialized.IsCompletedSuccessfully
            ? OnSetParametersFlow(true)
            : CompleteOnInitializedFlowAsync(whenInitialized);
    }

    private async Task CompleteOnInitializedFlowAsync(Task whenInitialized)
    {
        await whenInitialized.SuppressCancellationAwait(); // Blazor views lifecycle method cancellations as ~normal completions
        await OnSetParametersFlow(true);
    }

    private Task OnSetParametersFlow(bool isInitializing)
    {
        OnParametersSet();
        var whenParametersSet = OnParametersSetAsync();

        // Maybe render on sync part completion
        var option = isInitializing
            ? ComputedStateComponentOptions.RenderOnceInitializedAsync
            : ComputedStateComponentOptions.RenderOnceParametersSet;
        if ((Options & option) != 0)
            StateHasChanged();

        // Sync-async branching to speed up the "happy" sync path
        if (!whenParametersSet.IsCompletedSuccessfully)
            return CompleteOnSetParametersFlowAsync(whenParametersSet, isInitializing);

        // Maybe render on async part completion (there is no actual async part, but we act like it's there)
        if ((Options & ComputedStateComponentOptions.RenderOnceParametersSet) != 0)
            StateHasChanged();

        // The code below handles RecomputeStateOnParameterChange, and:
        // - If we're initializing, the State (re)computes anyway -> no recompute
        // - No RecomputeStateOnParameterChange -> no recompute
        if (isInitializing || (Options & ComputedStateComponentOptions.RecomputeStateOnParameterChange) == 0)
            return Task.CompletedTask;

        var whenComputed = State.Recompute();
        return whenComputed.IsCompleted
            ? Task.CompletedTask
            : whenComputed.AsTask().SuppressExceptions(); // Recompute errors are exposed via State.Value/Error
    }

    private async Task CompleteOnSetParametersFlowAsync(Task whenParametersSet, bool isInitializing)
    {
        await whenParametersSet.SuppressCancellationAwait(); // Blazor views lifecycle method cancellations as ~normal completions

        // Maybe render on async part completion (there is no actual async part, but we act like it's there)
        if ((Options & ComputedStateComponentOptions.RenderOnceParametersSetAsync) != 0)
            StateHasChanged();

        // The code below handles RecomputeStateOnParameterChange, and:
        // - If we're initializing, the State (re)computes anyway -> no recompute
        // - No RecomputeStateOnParameterChange -> no recompute
        if (isInitializing || (Options & ComputedStateComponentOptions.RecomputeStateOnParameterChange) == 0)
            return;

        var whenComputed = State.Recompute();
        if (!whenComputed.IsCompleted)
            await whenComputed.SilentAwait(); // Recompute errors are exposed via State.Value/Error
    }
}
