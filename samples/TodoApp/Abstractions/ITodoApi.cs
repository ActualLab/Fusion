using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.Fusion.Blazor;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

public interface ITodoApi : IComputeService
{
    // Commands
    [CommandHandler]
    public Task<TodoItem> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task Remove(Todos_Remove command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    public Task<TodoItem?> Get(Session session, Ulid id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default);
    [ComputeMethod(InvalidationDelay = 0.5)]
    public Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default);
}

// Data

[DataContract, MemoryPackable, MessagePackObject]
[ParameterComparer(typeof(ByValueParameterComparer))]
public sealed partial record TodoItem(
    [property: DataMember, Key(0)] Ulid Id,
    [property: DataMember, Key(1)] string Title,
    [property: DataMember, Key(2)] bool IsDone = false
);

[DataContract, MemoryPackable, MessagePackObject]
public sealed partial record TodoSummary(
    [property: DataMember, Key(0)] int Count,
    [property: DataMember, Key(1)] int DoneCount)
{
    public static readonly TodoSummary None = new(0, 0);
}

// Commands

[DataContract, MemoryPackable, MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_AddOrUpdate(
    [property: DataMember, Key(0)] Session Session,
    [property: DataMember, Key(1)] TodoItem Item
) : ISessionCommand<TodoItem>, IApiCommand;

[DataContract, MemoryPackable, MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_Remove(
    [property: DataMember, Key(0)] Session Session,
    [property: DataMember, Key(1)] Ulid Id
) : ISessionCommand<Unit>, IApiCommand;
