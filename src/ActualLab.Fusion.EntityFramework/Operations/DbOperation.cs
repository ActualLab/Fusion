using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

[Table("_Operations")]
[Index(nameof(Uuid), IsUnique = true)] // "Uuid -> Index" queries
[Index(nameof(LoggedAt))] // "LoggedAt > minLoggedAt -> min(Index)" queries + min(LoggedAt)
public sealed class DbOperation : IDbIndexedLogEntry
{
    public static ITextSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

    private long? _index;

    // DbOperations are never updated but only deleted, so Version and State properties aren't used
    long IDbLogEntry.Version { get => 0; set { } }
    LogEntryState IDbLogEntry.State { get => default; set { } }

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Index {
        get => _index ?? 0;
        set => _index = value;
    }
    [NotMapped] public bool HasIndex => _index.HasValue;

    public string Uuid { get; set; } = "";
    public string HostId { get; set; } = "";

    public DateTime LoggedAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    public string CommandJson { get; set; } = "";
    public string? ItemsJson { get; set; }
    public string? NestedOperations { get; set; }

    public DbOperation() { }
    public DbOperation(Operation operation)
        => UpdateFrom(operation);

    public Operation ToModel()
    {
        var command = CommandJson.IsNullOrEmpty()
            ? null
            : Serializer.Read<ICommand>(CommandJson);
        var items = ItemsJson.IsNullOrEmpty()
            ? default
            : Serializer.Read<PropertyBag>(ItemsJson);
        var nestedOperations = NestedOperations.IsNullOrEmpty()
            ? ImmutableList<NestedOperation>.Empty
            : Serializer.Read<ImmutableList<NestedOperation>>(NestedOperations);
        return new Operation(
            Uuid,
            HostId,
            LoggedAt,
            command!,
            items.ToMutable(),
            nestedOperations) {
            Index = HasIndex ? Index : null,
        };
    }

    public DbOperation UpdateFrom(Operation operation)
    {
        if (operation.Index is { } index)
            Index = index;
        Uuid = operation.Uuid;
        HostId = operation.HostId;
        LoggedAt = operation.LoggedAt;
        CommandJson = Serializer.Write(operation.Command);
        ItemsJson = operation.Items.Items.Count == 0 ? null : Serializer.Write(operation.Items.Snapshot);
        NestedOperations = operation.NestedOperations.Count == 0 ? null : Serializer.Write(operation.NestedOperations);
        return this;
    }
}
