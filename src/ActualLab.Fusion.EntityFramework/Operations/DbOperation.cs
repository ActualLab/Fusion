using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ActualLab.Fusion.Operations.Internal;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

#pragma warning disable IL2026

[Table("_Operations")]
[Index(nameof(Id), nameof(Index), Name = "IX_OperationId")]
[Index(nameof(StartTime), nameof(Index), Name = "IX_StartTime")]
[Index(nameof(CommitTime), nameof(Index), Name = "IX_CommitTime")]
public class DbOperation : IHasId<long>, IHasId<string>
{
    public static ITextSerializer Serializer { get; set; } = new NewtonsoftJsonSerializer();

    private long? _index;
    private DateTime _startTime;
    private DateTime _commitTime;

    long IHasId<long>.Id => Index;
    string IHasId<string>.Id => Id;

    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Index {
        get => _index ?? 0;
        set => _index = value;
    }
    [NotMapped]
    public bool HasIndex => _index.HasValue;

    public string Id { get; set; } = "";
    public string AgentId { get; set; } = "";

    public DateTime StartTime {
        get => _startTime.DefaultKind(DateTimeKind.Utc);
        set => _startTime = value.DefaultKind(DateTimeKind.Utc);
    }

    public DateTime CommitTime {
        get => _commitTime.DefaultKind(DateTimeKind.Utc);
        set => _commitTime = value.DefaultKind(DateTimeKind.Utc);
    }

    public string CommandJson { get; set; } = "";
    public string ItemsJson { get; set; } = "";
    public string NestedOperationsJson { get; set; } = "";

    public virtual Operation ToModel()
    {
        var command = CommandJson.IsNullOrEmpty()
            ? null
            : Serializer.Read<ICommand>(CommandJson);
        var items = ItemsJson.IsNullOrEmpty()
            ? new()
            : Serializer.Read<OptionSet>(ItemsJson);
        var nestedOperations = NestedOperationsJson.IsNullOrEmpty()
            ? new()
            : Serializer.Read<List<NestedOperation>>(NestedOperationsJson);
        return new Operation(Id, AgentId, StartTime, CommitTime, command!, items, nestedOperations) {
            Index = HasIndex ? Index : null,
        };
    }

    public virtual void UpdateFrom(Operation operation)
    {
        if (operation.Index is { } index)
            Index = index;
        Id = operation.Id;
        AgentId = operation.AgentId;
        StartTime = operation.StartTime;
        CommitTime = operation.CommitTime;
        CommandJson = Serializer.Write(operation.Command);
        ItemsJson = operation.Items.Items.Count == 0 ? "" : Serializer.Write(operation.Items);
        NestedOperationsJson = operation.NestedOperations.Count == 0 ? "" : Serializer.Write(operation.NestedOperations);
    }
}
