using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// Entity Framework entity representing a persisted operation in the "_Operations" table,
/// used for cross-host operation log replication and invalidation.
/// </summary>
[Table("_Operations")]
[Index(nameof(Uuid), IsUnique = true)] // "Uuid -> Index" queries
[Index(nameof(LoggedAt))] // "LoggedAt > minLoggedAt -> min(Index)" queries + min(LoggedAt)
public sealed class DbOperation : IDbIndexedLogEntry
{
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

    // Each payload has both columns; DbLogEntrySerializer.Format decides which one is
    // written, and a read takes whichever one carries the payload
    public string? CommandJson { get; set; }
    public byte[]? CommandData { get; set; }
    public string? InvalidationCallsJson { get; set; }
    public byte[]? InvalidationCallsData { get; set; }

    public DbOperation() { }
    public DbOperation(Operation operation, DbLogEntrySerializer? serializer = null)
        => UpdateFrom(operation, serializer);

    public Operation ToModel(DbLogEntrySerializer? serializer = null)
    {
        serializer ??= DbLogEntrySerializer.Default;
        var command = serializer.Deserialize<ICommand>(CommandJson, CommandData);
        // An array, not the ImmutableList the model uses: MessagePack's standard resolvers
        // don't know the immutable collections
        var invalidationCalls = serializer
            .Deserialize<ServiceCall[]>(InvalidationCallsJson, InvalidationCallsData);
        var operation = new Operation(Uuid, HostId, LoggedAt, command!) {
            Index = HasIndex ? Index : null,
        };
        return invalidationCalls is null or { Length: 0 }
            ? operation
            : operation.AddInvalidationCalls(invalidationCalls);
    }

    public DbOperation UpdateFrom(Operation operation, DbLogEntrySerializer? serializer = null)
    {
        serializer ??= DbLogEntrySerializer.Default;
        if (operation.Index is { } index)
            Index = index;
        Uuid = operation.Uuid;
        HostId = operation.HostId;
        LoggedAt = operation.LoggedAt;
        (CommandJson, CommandData) = serializer.Serialize(operation.Command);
        // Both columns stay null when there are no calls - the common case, and the reason
        // ToModel can skip deserializing anything at all for it
        (InvalidationCallsJson, InvalidationCallsData) = Serialize(serializer, operation.InvalidationCalls);
        return this;
    }

    // Private methods

    private static (string? Text, byte[]? Data) Serialize(
        DbLogEntrySerializer serializer, ImmutableList<ServiceCall> calls)
        => calls.Count == 0
            ? default
            : serializer.Serialize(calls.ToArray());
}
