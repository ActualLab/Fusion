using ActualLab.Generators;
using ActualLab.Versioning;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Represents an event recorded during an <see cref="Operation"/>,
/// typically used for eventual consistency and event replay.
/// </summary>
public sealed class OperationEvent(string uuid, object? value) : IHasUuid, IHasId<string>
{
    public static UuidGenerator UuidGenerator { get; set; } = UlidUuidGenerator.Instance;

    string IHasId<string>.Id => Uuid;
    // Persisted to DB
    public string Uuid { get; set; } = uuid;
    public object? Value { get; set; } = value;
    public Moment LoggedAt { get; set; }
    public Moment DelayUntil { get => Moment.Max(LoggedAt, field); set; }

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

    public OperationEvent SetDelayUntil(Moment delayUntil)
    {
        DelayUntil = delayUntil;
        return this;
    }

    public OperationEvent SetDelayUntil(Moment delayUntil, TimeSpan delayQuanta, string? uuidPrefix = null)
    {
        if (delayQuanta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delayQuanta));

        DelayUntil = delayUntil.Ceiling(delayQuanta);
        Uuid = ProvideDelayBasedUuid(uuidPrefix);
        UuidConflictStrategy = KeyConflictStrategy.Skip;
        return this;
    }

    public OperationEvent SetDelayBy(TimeSpan delayBy)
    {
        DelayUntil = LoggedAt + delayBy;
        return this;
    }

    public OperationEvent SetDelayBy(TimeSpan delayBy, TimeSpan delayQuanta, string? uuidPrefix = null)
    {
        if (delayQuanta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delayQuanta));

        DelayUntil = (LoggedAt + delayBy).Ceiling(delayQuanta);
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
    {
        uuidPrefix ??= Uuid;
        return $"{uuidPrefix}-at-{DelayUntil.EpochOffsetTicks:x}";
    }
}
