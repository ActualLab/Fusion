using System.Runtime.Serialization;
using MemoryPack;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public interface ITodoBackend : IComputeService
{
    // Commands
    [CommandHandler]
    Task<TodoItem> AddOrUpdate(TodoBackend_AddOrUpdate command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(TodoBackend_Remove command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    Task<TodoItem?> Get(string folder, Ulid id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Ulid[]> ListIds(string folder, int count, CancellationToken cancellationToken = default);
    [ComputeMethod(InvalidationDelay = 0.5)]
    Task<TodoSummary> GetSummary(string folder, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record TodoBackend_AddOrUpdate(
    [property: DataMember] string Folder,
    [property: DataMember] TodoItem Item
) : IBackendCommand<TodoItem>;

[DataContract, MemoryPackable]
// ReSharper disable once InconsistentNaming
public sealed partial record TodoBackend_Remove(
    [property: DataMember] string Folder,
    [property: DataMember] Ulid Id
) : IBackendCommand<Unit>;
