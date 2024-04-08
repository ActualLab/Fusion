using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_OperationEvents")]
[Index(nameof(Uuid), nameof(Index), Name = "IX_Uuid")]
[Index(nameof(LoggedAt), nameof(Index), Name = "IX_LoggedAt")]
public sealed class DbOperationEvent
    : ILogEntry, IHasId<string>, IHasId<long>
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private long? _index;
    private Symbol _uuid;
    private DateTime _loggedAt;

    Symbol IHasUuid.Uuid => _uuid;
    string IHasId<string>.Id => _uuid.Value;
    long IHasId<long>.Id => Index;
    // DbOperations are never updated, but only deleted, so...
    bool ILogEntry.IsProcessed { get => false; set { } }

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Index {
        get => _index ?? 0;
        set => _index = value;
    }
    [NotMapped] public bool HasIndex => _index.HasValue;

    public string Uuid { get => _uuid; set => _uuid = value; }
    [ConcurrencyCheck] public long Version { get; set; }

    public DateTime LoggedAt {
        get => _loggedAt.DefaultKind(DateTimeKind.Utc);
        set => _loggedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public string ValueJson { get; set; } = "";

    public DbOperationEvent() { }
    public DbOperationEvent(OperationEvent @event)
        => UpdateFrom(@event);

    public OperationEvent ToModel()
    {
        var value = ValueJson.IsNullOrEmpty()
            ? null
            : Serializer.Read(ValueJson, typeof(object));
        return new OperationEvent(Uuid, LoggedAt, value) {
            Index = HasIndex ? Index : null,
        };
    }

    public DbOperationEvent UpdateFrom(OperationEvent @event)
    {
        if (@event.Index is { } index)
            Index = index;
        Uuid = @event.Uuid;
        LoggedAt = @event.LoggedAt;
        ValueJson = Serializer.Write(@event.Value, typeof(object));
        return this;
    }
}
