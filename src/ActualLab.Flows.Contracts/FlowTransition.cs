
using ActualLab.CommandR.Operations;
using ActualLab.Flows.Infrastructure;

namespace ActualLab.Flows;

[StructLayout(LayoutKind.Auto)]
public readonly record struct FlowTransition(Flow Flow, Symbol Step)
{
    public bool MustStore { get; init; } = true;
    public bool MustWait { get; init; } = true;
    public Action<Operation>? EventBuilder { get; init; }

    public bool EffectiveMustStore
        => MustStore || Step == FlowSteps.MustRemove || EventBuilder != null;

    public override string ToString()
    {
        var flags = (EffectiveMustStore, MustWait) switch {
            (true, true) => "store, wait",
            (true, false) => "store",
            (false, true) => "no-store, wait",
            (false, false) => "no-store",
        };
        return $"->('{Step}', {flags})";
    }

    public FlowTransition AddEvents(Action<Operation> eventBuilder)
    {
        var oldEventBuilder = EventBuilder;
        return this with {
            MustStore = true,
            EventBuilder = operation => {
                oldEventBuilder?.Invoke(operation);
                eventBuilder.Invoke(operation);
            }
        };
    }

    public FlowTransition AddTimerEvent(TimeSpan delay, string? tag = null)
    {
        var uuid = ((IWorkerFlow)Flow).Worker.Require().Host.Commander.Hub.UuidGenerator.Next();
        var timerEvent = new FlowTimerEvent(uuid, Flow.Id, tag);
        return AddEvents(o => o.AddEvent(timerEvent, delay, uuid));
    }

    public FlowTransition AddTimerEvent(Moment firesAt, string? tag = null)
    {
        var uuid = ((IWorkerFlow)Flow).Worker.Require().Host.Commander.Hub.UuidGenerator.Next();
        var timerEvent = new FlowTimerEvent(uuid, Flow.Id, tag);
        return AddEvents(o => o.AddEvent(timerEvent, firesAt, uuid));
    }
}
