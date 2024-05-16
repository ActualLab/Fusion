
using ActualLab.CommandR.Operations;
using ActualLab.Flows.Infrastructure;

namespace ActualLab.Flows;

[StructLayout(LayoutKind.Auto)]
public readonly record struct FlowTransition(Flow Flow, Symbol Step)
{
    public bool IsStored { get; init; } = true;
    public bool IsEventual { get; init; } = true;
    public Action<Operation>? EventBuilder { get; init; }
    public bool EffectiveIsStored => IsStored || Step == FlowSteps.MustRemove || EventBuilder != null;

    public override string ToString()
    {
        var flags = (EffectiveIsStored, IsEventual) switch {
            (true, true) => "stored, eventual",
            (true, false) => "stored, immediate",
            (false, true) => "non-stored, eventual",
            (false, false) => "non-stored, immediate",
        };
        return $"->('{Step}', {flags})";
    }

    public FlowTransition AddEvents(Action<Operation> eventBuilder)
    {
        var oldEventBuilder = EventBuilder;
        return this with {
            IsStored = true,
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
