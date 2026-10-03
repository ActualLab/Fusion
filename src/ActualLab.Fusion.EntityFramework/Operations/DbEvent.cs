using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// Entity Framework entity representing a persisted operation event in the "_Events" table,
/// supporting delayed processing and state tracking.
/// </summary>
[Table("_Events")]
[Index(nameof(State), nameof(DelayUntil))] // "State == New & DelayUntil < now" queries
[Index(nameof(DelayUntil), nameof(State))] // "DelayUntil < trimAt && State != New" queries
public sealed class DbEvent : IDbEventLogEntry
{
    [Key] public string Uuid { get; set; } = "";

    [ConcurrencyCheck]
    public long Version { get; set; }

    public LogEntryState State { get; set; }

    public DateTime LoggedAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime DelayUntil {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    // Both columns exist; DbLogEntrySerializer.Format decides which one is written,
    // and a read takes whichever one carries the value
    public string? ValueJson { get; set; }
    public byte[]? ValueData { get; set; }

    public DbEvent() { }
    public DbEvent(
        OperationEvent model,
        VersionGenerator<long>? versionGenerator = null,
        DbLogEntrySerializer? serializer = null)
        => UpdateFrom(model, versionGenerator, serializer);

    // The operation's own row, written to the event log so that exactly one host replays it.
    // It keeps the "~op-" Uuid of the commit verifier it replaces - VerifyCommit looks the row up
    // by Uuid either way - but carries a value and stays New until processed.
    public DbEvent(
        Operation operation,
        object value,
        Moment delayUntil,
        VersionGenerator<long>? versionGenerator = null,
        DbLogEntrySerializer? serializer = null)
        : this(operation, versionGenerator)
    {
        (ValueJson, ValueData) = (serializer ?? DbLogEntrySerializer.Default).Serialize(value);
        DelayUntil = Moment.Max(operation.LoggedAt, delayUntil);
        State = LogEntryState.New;
    }

    // This constructor is used to create a fake DbEvent entry for an Operation.
    // The entry is used to verify whether the commit succeeded in case of an error during the commit.
    public DbEvent(Operation operation, VersionGenerator<long>? versionGenerator = null)
    {
        if (operation.Uuid.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(operation), "Uuid is empty.");

        Uuid = string.Concat("~op-", operation.Uuid);
        if (versionGenerator is not null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = operation.LoggedAt;
        // DelayUntil mirrors LoggedAt so this fake commit-verifier row survives the event trimmer's
        // MaxEntryAge (it trims "DelayUntil <= minDelayUntil && State != New") instead of being
        // trimmable from the moment it's committed.
        DelayUntil = operation.LoggedAt;
        State = LogEntryState.Processed;
    }

    public OperationEvent ToModel(DbLogEntrySerializer? serializer = null)
    {
        var value = (serializer ?? DbLogEntrySerializer.Default).Deserialize<object>(ValueJson, ValueData);
        return new OperationEvent(Uuid, value) {
            LoggedAt = LoggedAt,
            DelayUntil = DelayUntil,
        };
    }

    public DbEvent UpdateFrom(
        OperationEvent model,
        VersionGenerator<long>? versionGenerator = null,
        DbLogEntrySerializer? serializer = null)
    {
        if (model.Uuid.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(model), "Uuid is empty.");

        Uuid = model.Uuid;
        if (versionGenerator is not null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = model.LoggedAt;
        DelayUntil = model.DelayUntil;
        (ValueJson, ValueData) = (serializer ?? DbLogEntrySerializer.Default).Serialize(model.Value);
        return this;
    }
}
