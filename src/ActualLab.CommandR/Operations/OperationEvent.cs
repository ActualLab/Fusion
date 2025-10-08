using ActualLab.Generators;
using ActualLab.Versioning;

namespace ActualLab.CommandR.Operations;

public sealed class OperationEvent(string uuid, object? value) : IHasUuid, IHasId<string>
{
    public static UuidGenerator UuidGenerator { get; set; } = UlidUuidGenerator.Instance;

    string IHasId<string>.Id => Uuid;
    // Persisted to DB
    public string Uuid { get; set; } = uuid;
    public object? Value { get; set; } = value;
    public Moment LoggedAt { get; set; }
    public Moment DelayUntil { get => Moment.Max(LoggedAt, field); set; }
        = value is IHasDelayUntil hasDelayUntil ? hasDelayUntil.DelayUntil : default;

    // Non-persistent properties
    public KeyConflictStrategy UuidConflictStrategy { get; set; } = KeyConflictStrategy.Fail;

    public OperationEvent(object? value)
        : this(ProvideUuid(value), value)
    { }

    // SetXxx helpers

    public OperationEvent SetUuid(string uuid)
    {
        Uuid = uuid;
        return this;
    }

    public OperationEvent SetGeneratedUuid()
    {
        Uuid = UuidGenerator.Next();
        return this;
    }

    public OperationEvent SetValue(object? value)
    {
        Value = value;
        return this;
    }

    public OperationEvent SetLoggedAt(Moment loggedAt)
    {
        LoggedAt = loggedAt;
        return this;
    }

    public OperationEvent SetDelayBy(Moment delayUntil)
    {
        DelayUntil = delayUntil;
        return this;
    }

    public OperationEvent SetDelayUntil(Moment delayUntil, TimeSpan quanta, string? uuidPrefix = null)
    {
        DelayUntil = delayUntil.Ceiling(quanta);
        Uuid = ProvideDelayBasedUuid(uuidPrefix);
        UuidConflictStrategy = KeyConflictStrategy.Skip;
        return this;
    }

    public OperationEvent SetDelayBy(TimeSpan delayBy)
    {
        DelayUntil = LoggedAt + delayBy;
        return this;
    }

    public OperationEvent SetDelayBy(TimeSpan delayBy, TimeSpan quanta, string? uuidPrefix = null)
    {
        DelayUntil = (LoggedAt + delayBy).Ceiling(quanta);
        Uuid = ProvideDelayBasedUuid(uuidPrefix);
        UuidConflictStrategy = KeyConflictStrategy.Skip;
        return this;
    }

    public OperationEvent SetUuidConflictStrategy(KeyConflictStrategy uuidConflictStrategy)
    {
        UuidConflictStrategy = uuidConflictStrategy;
        return this;
    }

    // Private methods

    private static string ProvideUuid(object? value)
        => value is IHasUuid hasUuid && !hasUuid.Uuid.IsNullOrEmpty()
            ? hasUuid.Uuid
            : UuidGenerator.Next();

    private string ProvideDelayBasedUuid(string? uuidPrefix)
        => $"{uuidPrefix ?? Uuid}-at-{DelayUntil.EpochOffsetTicks:x}";
}
