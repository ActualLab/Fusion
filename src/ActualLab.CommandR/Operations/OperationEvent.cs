using ActualLab.Versioning;

namespace ActualLab.CommandR.Operations;

public sealed record OperationEvent(
    Symbol Uuid,
    Moment LoggedAt,
    Moment DelayUntil,
    object? Value,
    KeyConflictStrategy UuidConflictStrategy
    ) : IHasUuid, IHasId<Symbol>
{
    Symbol IHasId<Symbol>.Id => Uuid;

    public OperationEvent(object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(default, default, default, value, uuidConflictStrategy) { }
    public OperationEvent(Moment delayUntil, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(default, default, delayUntil, value, uuidConflictStrategy) { }
    public OperationEvent(Symbol uuid, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(uuid, default, default, value, uuidConflictStrategy) { }
    public OperationEvent(Symbol uuid, Moment delayUntil, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(uuid, default, delayUntil, value, uuidConflictStrategy) { }
}
