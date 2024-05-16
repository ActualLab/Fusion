using ActualLab.Flows;

namespace ActualLab.Fusion.Tests.Flows;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public partial class TimerFlow : Flow
{
    public override FlowOptions GetOptions()
        => new() { RemoveDelay = TimeSpan.FromSeconds(1) };

    protected override ValueTask ApplyTransition(FlowTransition transition, CancellationToken cancellationToken)
    {
        var output = Host.Services.GetRequiredService<ITestOutputHelper>();
        output.WriteLine($"'{Step}' {transition}");
        return base.ApplyTransition(transition, cancellationToken);
    }

    protected override async Task<FlowTransition> OnStart(CancellationToken cancellationToken)
    {
        var output = Host.Services.GetRequiredService<ITestOutputHelper>();
        output.WriteLine(nameof(OnStart));
        // return Goto(nameof(OnEnd)) with { IsStored = false, IsEventual = false };
        return Goto(nameof(OnTimer)).AddTimerEvent(TimeSpan.FromSeconds(3), "+");
    }

    protected async Task<FlowTransition> OnTimer(CancellationToken cancellationToken)
    {
        var timerEvent = Event.Require<FlowTimerEvent>();
        var output = Host.Services.GetRequiredService<ITestOutputHelper>();
        output.WriteLine($"{nameof(OnTimer)}: {timerEvent}");

        var nextTag = timerEvent.Tag + "+";
        return nextTag.Length <= 2
            ? Goto(nameof(OnTimer)).AddTimerEvent(TimeSpan.FromSeconds(5), nextTag)
            : Goto(nameof(OnEnd)) with { IsStored = false, IsEventual = false };
    }
}
