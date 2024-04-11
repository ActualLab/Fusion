using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_OperationEvents")]
[Index(nameof(Uuid), IsUnique = true)] // "Uuid -> Index" queries
[Index(nameof(State), nameof(LoggedAt))] // "!IsProcessed -> min(Index)" queries
[Index(nameof(LoggedAt))] // "LoggedAt > minLoggedAt -> min(Index)" queries + min(LoggedAt)
public sealed class DbOperationEvent : IDbIndexedLogEntry
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private long? _index;
    private DateTime _loggedAt;

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Index {
        get => _index ?? 0;
        set => _index = value;
    }
    [NotMapped] public bool HasIndex => _index.HasValue;

    public string Uuid { get; set; } = "";
    [ConcurrencyCheck] public long Version { get; set; }

    public DateTime LoggedAt {
        get => _loggedAt.DefaultKind(DateTimeKind.Utc);
        set => _loggedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public string ValueJson { get; set; } = "";
    public LogEntryState State { get; set; }

    public DbOperationEvent() { }
    public DbOperationEvent(OperationEvent model, VersionGenerator<long> versionGenerator)
        => UpdateFrom(model, versionGenerator);

    public OperationEvent ToModel()
    {
        var value = ValueJson.IsNullOrEmpty()
            ? null
            : Serializer.Read(ValueJson, typeof(object));
        return new OperationEvent(Uuid, default, value);
    }

    public DbOperationEvent UpdateFrom(OperationEvent model, VersionGenerator<long> versionGenerator)
    {
        Uuid = model.Uuid;
        Version = versionGenerator.NextVersion(Version);
        ValueJson = Serializer.Write(model.Value, typeof(object));
        return this;
    }
}
