using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_Operations")]
[Index(nameof(Uuid), nameof(Index), Name = "IX_Uuid")]
[Index(nameof(LoggedAt), nameof(Index), Name = "IX_LoggedAt")]
public sealed class DbOperation
    : ILogEntry, IHasId<string>, IHasId<long>
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private long? _index;
    private Symbol _uuid;
    private Symbol _hostId;
    private DateTime _startedAt;
    private DateTime _completedAt;

    Symbol IHasUuid.Uuid => _uuid;
    string IHasId<string>.Id => _uuid.Value;
    long IHasId<long>.Id => Index;
    // DbOperations are never updated, but only deleted, so...
    long ILogEntry.Version { get => 0; set { } }
    bool ILogEntry.IsProcessed { get => false; set { } }

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Index {
        get => _index ?? 0;
        set => _index = value;
    }
    [NotMapped] public bool HasIndex => _index.HasValue;

    public string Uuid { get => _uuid; set => _uuid = value; }
    public string HostId { get => _hostId; set => _hostId = value; }

    public DateTime StartedAt {
        get => _startedAt.DefaultKind(DateTimeKind.Utc);
        set => _startedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime LoggedAt {
        get => _completedAt.DefaultKind(DateTimeKind.Utc);
        set => _completedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public string CommandJson { get; set; } = "";
    public string ItemsJson { get; set; } = "";
    public string NestedOperations { get; set; } = "";

    public DbOperation() { }
    public DbOperation(Operation operation)
        => UpdateFrom(operation);

    public Operation ToModel()
    {
        var command = CommandJson.IsNullOrEmpty()
            ? null
            : Serializer.Read<ICommand>(CommandJson);
        var items = ItemsJson.IsNullOrEmpty()
            ? new()
            : Serializer.Read<OptionSet>(ItemsJson);
        var nestedCommands = NestedOperations.IsNullOrEmpty()
            ? new()
            : Serializer.Read<List<NestedOperation>>(NestedOperations);
        return new Operation(
            Uuid,
            HostId,
            StartedAt,
            LoggedAt,
            command!,
            items,
            nestedCommands) {
            Index = HasIndex ? Index : null,
        };
    }

    public DbOperation UpdateFrom(Operation operation)
    {
        if (operation.Index is { } index)
            Index = index;
        Uuid = operation.Uuid;
        HostId = operation.HostId;
        StartedAt = operation.StartedAt;
        LoggedAt = operation.LoggedAt;
        CommandJson = Serializer.Write(operation.Command);
        ItemsJson = operation.Items.Items.Count == 0 ? "" : Serializer.Write(operation.Items);
        NestedOperations = operation.NestedOperations.Count == 0 ? "" : Serializer.Write(operation.NestedOperations);
        return this;
    }
}
