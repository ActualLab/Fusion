using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_Events")]
[Index(nameof(Uuid), IsUnique = true)] // "Uuid -> Index" queries
[Index(nameof(State), nameof(DelayUntil))] // "!IsProcessed & DelayUntil < now" queries
[Index(nameof(DelayUntil))] // "DelayUntil < trimAt" queries
public sealed class DbEvent : IDbEventLogEntry
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private DateTime _loggedAt;
    private DateTime _delayUntil;

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Uuid { get; set; } = "";

    [ConcurrencyCheck]
    public long Version { get; set; }

    public DateTime LoggedAt {
        get => _loggedAt.DefaultKind(DateTimeKind.Utc);
        set => _loggedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime DelayUntil {
        get => _delayUntil.DefaultKind(DateTimeKind.Utc);
        set => _delayUntil = value.DefaultKind(DateTimeKind.Utc);
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
        return new OperationEvent(Uuid, LoggedAt, DelayUntil, value);
    }

    public DbEvent UpdateFrom(OperationEvent model, VersionGenerator<long>? versionGenerator = null)
    {
        Uuid = model.Uuid;
        if (versionGenerator != null)
            Version = versionGenerator.NextVersion(Version);
        LoggedAt = model.LoggedAt;
        DelayUntil = model.DelayUntil;
        ValueJson = Serializer.Write(model.Value, typeof(object));
        return this;
    }
}
