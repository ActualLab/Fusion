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

[DataContract, MemoryPackable, MessagePackObject(true)]
[ParameterComparer(typeof(ByValueParameterComparer))]
public sealed partial record TodoItem(
    [property: DataMember] Ulid Id,
    [property: DataMember] string Title,
    [property: DataMember] bool IsDone = false
);

[DataContract, MemoryPackable, MessagePackObject(true)]
public sealed partial record TodoSummary(
    [property: DataMember] int Count,
    [property: DataMember] int DoneCount)
{
    public static readonly TodoSummary None = new(0, 0);
}

// Commands

[DataContract, MemoryPackable, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_AddOrUpdate(
    [property: DataMember] Session Session,
    [property: DataMember] TodoItem Item
) : ISessionCommand<TodoItem>, IApiCommand;

[DataContract, MemoryPackable, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_Remove(
    [property: DataMember] Session Session,
    [property: DataMember] Ulid Id
) : ISessionCommand<Unit>, IApiCommand;
