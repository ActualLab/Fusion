using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.Fusion.Blazor;

namespace Samples.TodoApp.Abstractions;

public interface ITodoService : IComputeService
{
    // Commands
    [CommandHandler]
    Task<Todo> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(Todos_Remove command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    Task<Todo?> Get(Session session, Ulid id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default);
    [ComputeMethod(InvalidationDelay = 1)]
    Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable]
[ParameterComparer(typeof(ByValueParameterComparer))]
public sealed partial record Todo(
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
};

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_AddOrUpdate(
    [property: DataMember] Session Session,
    [property: DataMember] Todo Item
) : ISessionCommand<Todo>, IApiCommand;

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record Todos_Remove(
    [property: DataMember] Session Session,
    [property: DataMember] Ulid Id
) : ISessionCommand<Unit>, IApiCommand;
