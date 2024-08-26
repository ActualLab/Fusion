using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public class TodoService(IAuth auth, ITodoBackend backend, ICommander commander) : ITodoService
{
    // Commands

    public virtual async Task<Todo> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (session, todo) = command;
        var folder = await GetFolder(session, cancellationToken);
        return await commander.Call(new TodoBackend_AddOrUpdate(folder, todo), cancellationToken);
    }

    public virtual async Task Remove(Todos_Remove command, CancellationToken cancellationToken = default)
    {
        var (session, id) = command;
        var folder = await GetFolder(session, cancellationToken);
        await commander.Call(new TodoBackend_Remove(folder, id), cancellationToken);
    }

    // Queries

    public virtual async Task<Todo?> Get(Session session, Ulid id, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken);
        var todo = await backend.Get(folder, id, cancellationToken);
#if false // Change to true to see how the error is processed in UI
        if (todo?.Title.Contains("err") == true)
            throw new InvalidOperationException("Error!");
#endif
        return todo;
    }

    public virtual async Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken);
        return await backend.ListIds(folder, count, cancellationToken);
    }

    public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken);
        return await backend.GetSummary(folder, cancellationToken);
    }

    // Private methods

    private async ValueTask<string> GetFolder(Session session, CancellationToken cancellationToken)
    {
        // tenant is associated with a session
        var tenant = session.GetTenant();
        var user = await auth.GetUser(session, cancellationToken);
        return user != null
            ? $"{tenant.Id}/user/{user.Id}"
            : $"{tenant.Id}/global";
    }
}
