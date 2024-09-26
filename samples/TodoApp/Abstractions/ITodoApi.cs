using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.Fusion.Blazor;

namespace Samples.TodoApp.Abstractions;

public interface ITodoApi : IComputeService
{
    // Commands
    [CommandHandler]
    Task<TodoItem> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(Todos_Remove command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    Task<TodoItem?> Get(Session session, Ulid id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default);
    [ComputeMethod(InvalidationDelay = 0.5)]
    Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default);
}

// Data

[DataContract, MemoryPackable]
[ParameterComparer(typeof(ByValueParameterComparer))]
public sealed partial record TodoItem(
    [property: DataMember] Ulid Id,
    [property: DataMember] string Title,
    [property: DataMember] bool IsDone = false
);

[DataContract, MemoryPackable]
public sealed partial record TodoSummary(
    [property: DataMember] int Count,
    [property: DataMember] int DoneCount)
{
    public static readonly TodoSummary None = new(0, 0);
}

// Commands

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_AddOrUpdate(
    [property: DataMember] Session Session,
    [property: DataMember] TodoItem Item
) : ISessionCommand<TodoItem>, IApiCommand;

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_Remove(
    [property: DataMember] Session Session,
    [property: DataMember] Ulid Id
) : ISessionCommand<Unit>, IApiCommand;
