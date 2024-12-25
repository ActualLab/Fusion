using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public interface ITodoBackend : IComputeService
{
    // Commands
    [CommandHandler]
    public Task<TodoItem> AddOrUpdate(TodoBackend_AddOrUpdate command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task Remove(TodoBackend_Remove command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    public Task<TodoItem?> Get(string folder, Ulid id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<Ulid[]> ListIds(string folder, int count, CancellationToken cancellationToken = default);
    [ComputeMethod(InvalidationDelay = 0.5)]
    public Task<TodoSummary> GetSummary(string folder, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public sealed partial record TodoBackend_AddOrUpdate(
    [property: DataMember] string Folder,
    [property: DataMember] TodoItem Item
) : IBackendCommand<TodoItem>;

[DataContract, MemoryPackable, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public sealed partial record TodoBackend_Remove(
    [property: DataMember] string Folder,
    [property: DataMember] Ulid Id
) : IBackendCommand<Unit>;
