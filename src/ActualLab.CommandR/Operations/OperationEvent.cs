using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Operations;

public class OperationEvent(
    Symbol uuid,
    Moment loggedAt = default,
    object? value = null
    ) : IHasUuid, IHasId<Symbol>
{
    Symbol IHasId<Symbol>.Id => Uuid;

    public long? Index { get; set; }
    public Symbol Uuid { get; set; } = uuid;
    public Moment LoggedAt { get; set; } = loggedAt;
    public object? Value { get; set; } = value;

    public static OperationEvent New(IOperationScope scope, object? value, Symbol uuid = default)
    {
        if (scope is not { AllowsEvents: true })
            throw Errors.ThisOperationCannotHaveEvents();

        var commanderHub = scope.CommandContext.Commander.Hub;
        var clock = commanderHub.Clocks.SystemClock;
        if (uuid.IsEmpty) {
            if (value is IHasUuid hasUuid)
                uuid = hasUuid.Uuid;
            else
                uuid = commanderHub.UuidGenerator.Next();
        }
        return new OperationEvent(uuid, clock.Now, value);
    }

    public OperationEvent()
        : this(default)
    { }
}
