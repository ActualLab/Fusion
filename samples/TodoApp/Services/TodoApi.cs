using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public class TodoApi(IAuth auth, ITodoBackend backend, ICommander commander) : ITodoApi
{
    // Commands

    public virtual async Task<TodoItem> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (session, todo) = command;
        var folder = await GetFolder(session, cancellationToken).ConfigureAwait(false);
        return await commander.Call(new TodoBackend_AddOrUpdate(folder, todo), cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task Remove(Todos_Remove command, CancellationToken cancellationToken = default)
    {
        var (session, id) = command;
        var folder = await GetFolder(session, cancellationToken).ConfigureAwait(false);
        await commander.Call(new TodoBackend_Remove(folder, id), cancellationToken).ConfigureAwait(false);
    }

    // Queries

    public virtual async Task<TodoItem?> Get(Session session, Ulid id, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken).ConfigureAwait(false);
        var item = await backend.Get(folder, id, cancellationToken).ConfigureAwait(false);
#if false // Change to true to see how the error is processed in UI
        if (todo?.Title.Contains("err") == true)
            throw new InvalidOperationException("Error!");
#endif
        return item;
    }

    public virtual async Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken).ConfigureAwait(false);
        return await backend.ListIds(folder, count, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
    {
        var folder = await GetFolder(session, cancellationToken).ConfigureAwait(false);
        return await backend.GetSummary(folder, cancellationToken).ConfigureAwait(false);
    }

    // Private methods

    private async ValueTask<string> GetFolder(Session session, CancellationToken cancellationToken)
    {
        var user = await auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        var tenant = session.GetTenant(); // tenant is associated with a session
        return user != null
            ? $"{tenant}/user/{user.Id}"
            : $"{tenant}/global";
    }
}
