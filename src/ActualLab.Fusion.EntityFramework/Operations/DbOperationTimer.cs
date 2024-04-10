using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_OperationTimers")]
[Index(nameof(State), nameof(FiresAt), Name = "IX_State2")] // "!IsProcessed & FiresAt < now" queries
[Index(nameof(LoggedAt), Name = "IX_LoggedAt")] // "LoggedAt < trimAt" queries
[Index(nameof(FiresAt), Name = "IX_FiresAt")] // "FiresAt < trimAt" queries
public sealed class DbOperationTimer : ILogEntry, IHasId<string>
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private Symbol _uuid;
    private DateTime _loggedAt;
    private DateTime _firesAt;

    Symbol IHasUuid.Uuid => _uuid;
    string IHasId<string>.Id => _uuid.Value;
    long ILogEntry.Index => 0;

    [Key] public string Uuid { get => _uuid; set => _uuid = value; }
    [ConcurrencyCheck] public long Version { get; set; }

    public DateTime LoggedAt {
        get => _loggedAt.DefaultKind(DateTimeKind.Utc);
        set => _loggedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime FiresAt {
        get => _firesAt.DefaultKind(DateTimeKind.Utc);
        set => _firesAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public string ValueJson { get; set; } = "";
    public LogEntryState State { get; set; }

    public DbOperationTimer() { }
    public DbOperationTimer(OperationEvent model, VersionGenerator<long> versionGenerator)
        => UpdateFrom(model, versionGenerator);

    public OperationEvent ToModel()
    {
        var value = ValueJson.IsNullOrEmpty()
            ? null
            : Serializer.Read(ValueJson, typeof(object));
        return new OperationEvent(Uuid, LoggedAt, FiresAt, value);
    }

    public DbOperationTimer UpdateFrom(OperationEvent model, VersionGenerator<long> versionGenerator)
    {
        Uuid = model.Uuid;
        LoggedAt = model.LoggedAt;
        FiresAt = model.FiresAt;
        ValueJson = Serializer.Write(model.Value, typeof(object));
        Version = versionGenerator.NextVersion(Version);
        return this;
    }
}
