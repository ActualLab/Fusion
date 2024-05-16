using ActualLab.Flows;

namespace ActualLab.Fusion.Tests.Flows;

public class TimerFlow : Flow
{
    public override FlowOptions GetOptions()
        => new() { RemoveDelay = TimeSpan.FromSeconds(1) };

    protected override async Task<FlowTransition> OnStart(CancellationToken cancellationToken)
    {
        Worker.Log.LogInformation(nameof(OnStart));
        await Task.Yield();
        return Goto("Timer").AddTimerEvent(TimeSpan.FromSeconds(5), "");
    }

    protected async Task<FlowTransition> OnTimer(CancellationToken cancellationToken)
    {
        var timerEvent = Event.Require<FlowTimerEvent>();
        Worker.Log.LogInformation("OnTimer: {Event}", timerEvent);
        await Task.Yield();
        var nextTag = timerEvent.Tag + "+";
        return Goto("Timer").AddTimerEvent(TimeSpan.FromSeconds(5), nextTag);
    }
}
