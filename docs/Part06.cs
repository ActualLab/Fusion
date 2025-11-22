using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion;
using static System.Console;

namespace Tutorial06;
#region Part06_Initialize
// Note: This is illustrative code from StatefulComponentBase
public partial class StatefulComponentBaseExample
{
    protected object? State { get; private set; }

    protected void OnInitialized()
    {
        State ??= CreateState();
        // UntypedState.AddEventHandler(StateEventKind.All, StateChanged);
    }

    protected virtual object CreateState()
        => Services.GetRequiredService<object>();

    // Assuming Services is available
    protected IServiceProvider Services { get; } = null!;
}
#endregion

#region Part06_StateEventKind
// Note: This is illustrative code from StatefulComponentBase
public partial class StatefulComponentBaseExample
{
    protected StateEventKind StateHasChangedTriggers { get; set; } =
        StateEventKind.Updated;

    public StatefulComponentBaseExample()
    {
        // StateChanged = (_, eventKind) => {
        //     if ((eventKind & StateHasChangedTriggers) == 0)
        //         return;
        //     this.NotifyStateHasChanged();
        // };
    }
}
#endregion

public static class Part06
{
    public static async Task Run()
    {
        WriteLine("Part 6: Real-time UI in Blazor Apps");
        WriteLine("This part covers Blazor components and doesn't have executable console examples.");
        await Task.CompletedTask;
    }
}
