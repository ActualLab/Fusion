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
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

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

    public string ValueJson { get; set; } = "";

    public DbEvent() { }
    public DbEvent(OperationEvent model, VersionGenerator<long>? versionGenerator = null)
        => UpdateFrom(model, versionGenerator);

    // This constructor is used to create a fake DbEvent entry for an Operation.
    // The entry is used to verify whether the commit succeeded in case of an error during the commit.
    public DbEvent(Operation model, VersionGenerator<long>? versionGenerator = null)
    {
        if (model.Uuid.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(model), "Uuid is empty.");

        Uuid = string.Concat("~op-", model.Uuid);
        if (versionGenerator is not null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = model.LoggedAt;
        State = LogEntryState.Processed;
    }

    public OperationEvent ToModel()
    {
        var value = ValueJson.IsNullOrEmpty()
            ? null
            : Serializer.Read(ValueJson, typeof(object));

        return new OperationEvent(Uuid, value) {
            LoggedAt = LoggedAt,
            DelayUntil = DelayUntil,
        };
    }

    public DbEvent UpdateFrom(OperationEvent model, VersionGenerator<long>? versionGenerator = null)
    {
        if (model.Uuid.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(model), "Uuid is empty.");

        Uuid = model.Uuid;
        if (versionGenerator is not null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = model.LoggedAt;
        DelayUntil = model.DelayUntil;
        ValueJson = Serializer.Write(model.Value, typeof(object));
        return this;
    }
}
