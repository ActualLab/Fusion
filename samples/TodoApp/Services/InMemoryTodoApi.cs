using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

#pragma warning disable 1998

public class InMemoryTodoApi : ITodoApi
{
    private ImmutableList<TodoItem> _items = ImmutableList<TodoItem>.Empty;

    // Commands

    public virtual async Task<TodoItem> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (session, todo) = command;
        if (todo.Id == Ulid.Empty)
            todo = todo with { Id = Ulid.NewUlid() };
        var oldItems = _items;
        var oldItem = oldItems.FirstOrDefault(i => i.Id == todo.Id);
        var items = _items = oldItems.RemoveAll(i => i.Id == todo.Id).Add(todo);

        using var invalidating = Invalidation.Begin();
        // Invalidation logic
        _ = Get(session, todo.Id, default);
        if (items.Count != oldItems.Count) {
            _ = PseudoListIds(session);
            _ = GetSummary(session, default);
        }
        else if (todo.IsDone != oldItem?.IsDone)
            _ = GetSummary(session, default);
        return todo;
    }

    public virtual async Task Remove(Todos_Remove command, CancellationToken cancellationToken = default)
    {
        var (session, id) = command;
        var oldItems = _items;
        var items = _items = oldItems.RemoveAll(i => i.Id == id);
        if (items == oldItems)
            return; // Nothing to invalidate

        using var invalidating = Invalidation.Begin();
        // Invalidation logic
        _ = Get(session, id, default);
        _ = PseudoListIds(session);
        _ = GetSummary(session, default);
    }

    // Queries

    public virtual Task<TodoItem?> Get(Session session, Ulid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.SingleOrDefault(i => i.Id == id));

    public virtual async Task<Ulid[]> ListIds(Session session, int count, CancellationToken cancellationToken = default)
    {
        await PseudoListIds(session).ConfigureAwait(false);
        return _items.Select(x => x.Id).Order().Take(count).ToArray();
    }

    public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
    {
        var count = _items.Count;
        var doneCount = _items.Count(i => i.IsDone);
        return new TodoSummary(count, doneCount);
    }

    // Pseudo queries

    // This is a "pseudo access" method that's called solely to become a dependency.
    // When it gets invalidated, it also invalidates all ListIds(session, <any_limit>) at once.
    // See the places it's called from to understand how it works.
    [ComputeMethod]
    protected virtual Task<Unit> PseudoListIds(Session session)
        => TaskExt.UnitTask;
}
