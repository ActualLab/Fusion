using ActualLab.Versioning;

namespace ActualLab.CommandR.Operations;

public sealed record OperationEvent(
    string Uuid,
    Moment LoggedAt,
    Moment DelayUntil,
    object? Value,
    KeyConflictStrategy UuidConflictStrategy
    ) : IHasUuid, IHasId<string>
{
    string IHasId<string>.Id => Uuid;

    public OperationEvent(object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this("", default, default, value, uuidConflictStrategy) { }
    public OperationEvent(Moment delayUntil, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this("", default, delayUntil, value, uuidConflictStrategy) { }
    public OperationEvent(string uuid, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(uuid, default, default, value, uuidConflictStrategy) { }
    public OperationEvent(string uuid, Moment delayUntil, object? value, KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        : this(uuid, default, delayUntil, value, uuidConflictStrategy) { }
}
