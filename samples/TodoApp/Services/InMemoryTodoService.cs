using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

#pragma warning disable 1998

public class InMemoryTodoService : ITodoService
{
    private ImmutableList<Todo> _store = ImmutableList<Todo>.Empty;

    // Commands

    public virtual async Task<Todo> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (session, todo) = command;
        if (todo.Id == Ulid.Empty)
            todo = todo with { Id = Ulid.NewUlid() };
        _store = _store.RemoveAll(i => i.Id == todo.Id).Add(todo);

        using var invalidating = Invalidation.Begin();
        _ = Get(session, todo.Id, default);
        _ = PseudoGetAllItems(session);
        return todo;
    }

    public virtual async Task Remove(Todos_Remove command, CancellationToken cancellationToken = default)
    {
        var (session, id) = command;
        _store = _store.RemoveAll(i => i.Id == id);

        using var invalidating = Invalidation.Begin();
        _ = Get(session, id, default);
        _ = PseudoGetAllItems(session);
    }

    // Queries

    public virtual Task<Todo?> Get(Session session, Ulid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.SingleOrDefault(i => i.Id == id));

    public virtual async Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default)
    {
        await PseudoGetAllItems(session);
        return _store.Select(x => x.Id).Order().Take(count).ToArray();
    }

    public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
    {
        await PseudoGetAllItems(session);
        var count = _store.Count;
        var doneCount = _store.Count(i => i.IsDone);
        return new TodoSummary(count, doneCount);
    }

    // Pseudo queries

    [ComputeMethod]
    protected virtual Task<Unit> PseudoGetAllItems(Session session)
        => TaskExt.UnitTask;
}
