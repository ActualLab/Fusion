
using ActualLab.CommandR.Operations;
using ActualLab.Flows.Infrastructure;

namespace ActualLab.Flows;

[StructLayout(LayoutKind.Auto)]
public readonly record struct FlowTransition(Flow Flow, Symbol Step)
{
    public bool MustSave { get; init; } = true;
    public bool IsImmediate { get; init; } = false;
    public Action<Operation>? EventBuilder { get; init; }

    public override string ToString()
        => $"->{Step.Value}(MustSave: {MustSave}, IsImmediate: {IsImmediate})";

    public FlowTransition AddEvents(Action<Operation> eventBuilder)
    {
        var oldEventBuilder = EventBuilder;
        return this with {
            MustSave = true,
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
