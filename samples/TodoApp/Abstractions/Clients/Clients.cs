using RestEase;
using Stl.Fusion.Extensions;

namespace Templates.TodoApp.Abstractions.Clients;

[BasePath("todo")]
public interface ITodoClientDef
{
    [Post(nameof(AddOrUpdate))]
    Task<Todo> AddOrUpdate([Body] AddOrUpdateTodoCommand command, CancellationToken cancellationToken = default);
    [Post(nameof(Remove))]
    Task Remove([Body] RemoveTodoCommand command, CancellationToken cancellationToken = default);

    [Get(nameof(Get))]
    Task<Todo?> Get(Session session, string id, CancellationToken cancellationToken = default);
    [Get(nameof(List))]
    Task<Todo[]> List(Session session, PageRef<string> pageRef, CancellationToken cancellationToken = default);
    [Get(nameof(GetSummary))]
    Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default);
}
