namespace ActualLab.Flows;

[StructLayout(LayoutKind.Auto)]
public struct FlowEventSource(Flow flow, object? @event)
{
    public Flow Flow { get; } = flow;
    public object? Event { get; private set; } = @event;
    public bool IsUsed => ReferenceEquals(Event, null);

    public TEvent Require<TEvent>()
        where TEvent : class
        => Is<TEvent>(out var @event)
            ? @event
            : throw Internal.Errors.NoEvent(Flow.GetType(), Flow.Step, typeof(TEvent));

    public TEvent? As<TEvent>()
        where TEvent : class
        => Is<TEvent>(out var @event)
            ? @event
            : null;

    public bool Is<TEvent>(out TEvent @event)
        where TEvent : class
    {
        if (Event is TEvent e) {
            @event = e;
            MarkUsed();
            return true;
        }

        @event = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkUsed()
        => Event = null;
}
