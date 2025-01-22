using ActualLab.Fusion.Blazor;
using static System.Console;

namespace Tutorial;

public static class Part06{
    public abstract class StatefulComponentBase<TState>:StatefulComponentBase
    where TState:class,IState
    {
        #region Part06_Initialize
        protected TState State {get;private set;} = null;
        protected override void OnInitialized(){
             State ??=CreateState();
            UntypedState.AddEventHandler(StateEventKind.All,StateChanged);
        }
        protected virtual TState CreateState()
        => Services.GetRequiredService<TState>();
        #endregion

        #region Part06_StateEventKind
        protected StateEventKind StateHasChangedTriggers {get;set;} =
        StateEventKind.Updated;

        protected StatefulComponentBase()
        {
            StateChanged = (_,eventKind) => {
                if((eventKind & StateHasChangedTriggers) == 0)
                return;
                this.NotifyStateHasChanged();
            };
        }
        #endregion
    }
    
    public abstract class ComputedStateComponent<TState>:StatefulComponentBase<IComputedState<TState>>
    {
        protected ComputedStateComponentOptions Options {get;set;} = ComputedStateComponent.DefaultOptions;
        protected override Task OnParametersSetAsync(){
            if (0 ==(Options & ComputedStateComponentOptions.RecomputeStateOnParameterChange))
                return Task.CompletedTask;
            State.Recompute();
            return Task.CompletedTask;
        }
        protected virtual ComputedState<TState>.Options GetStateOptions() => new();
    }
}
