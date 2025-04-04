using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

[Table("_Events")]
[Index(nameof(State), nameof(DelayUntil))] // "!IsProcessed & DelayUntil < now" queries
[Index(nameof(DelayUntil))] // "DelayUntil < trimAt" queries
public sealed class DbEvent : IDbEventLogEntry
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    [Key] public string Uuid { get; set; } = "";

    [ConcurrencyCheck]
    public long Version { get; set; }

    public DateTime LoggedAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime DelayUntil {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    public string ValueJson { get; set; } = "";
    public LogEntryState State { get; set; }

    public DbEvent() { }
    public DbEvent(OperationEvent model, VersionGenerator<long>? versionGenerator = null)
        => UpdateFrom(model, versionGenerator);

    public OperationEvent ToModel()
    {
        var value = ValueJson.IsNullOrEmpty()
            ? null
            : Serializer.Read(ValueJson, typeof(object));
        return new OperationEvent(Uuid, LoggedAt, DelayUntil, value, KeyConflictStrategy.Fail);
    }

    public DbEvent UpdateFrom(OperationEvent model, VersionGenerator<long>? versionGenerator = null)
    {
        if (model.Uuid.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(model), "Uuid is empty.");

        Uuid = model.Uuid;
        if (versionGenerator != null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = model.LoggedAt;
        DelayUntil = model.DelayUntil;
        ValueJson = Serializer.Write(model.Value, typeof(object));
        return this;
    }
}
