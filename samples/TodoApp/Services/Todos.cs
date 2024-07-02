using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.Extensions;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Templates.TodoApp.Abstractions;

namespace Templates.TodoApp.Services;

public class Todos(ISandboxedKeyValueStore store, IAuth auth) : ITodos
{
    public Task<RpcObjectId> GetTestObjectId()
        => Task.FromResult(new RpcObjectId(Guid.NewGuid(), 1));

    public Task<RpcStream<int>> GetTestStream()
        => Task.FromResult(new RpcStream<int>(Enumerable.Range(0, 5).ToAsyncEnumerable()));

    public Task<int> SumTestStream(RpcStream<int> stream, CancellationToken cancellationToken = default)
        => stream.SumAsync(cancellationToken).AsTask();

    // Commands

    public virtual async Task<Todo> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive)
            return default!;

        var (session, todo) = command;
        var user = await auth.GetUser(session, cancellationToken).Require();

        Todo? oldTodo = null;
        if (string.IsNullOrEmpty(todo.Id))
            todo = todo with { Id = Ulid.NewUlid().ToString() };
        else
            oldTodo = await Get(session, todo.Id, cancellationToken);

        if (todo.Title.Contains("@"))
            throw new InvalidOperationException("Todo title can't contain '@' symbol.");

        var key = GetTodoKey(user, todo.Id);
        await store.Set(session, key, todo, cancellationToken);
        if (oldTodo?.IsDone != todo.IsDone) {
            var doneKey = GetDoneKey(user, todo.Id);
            if (todo.IsDone)
                await store.Set(session, doneKey, true, cancellationToken);
            else
                await store.Remove(session, doneKey, cancellationToken);
        }

        if (todo.Title.Contains("#"))
            throw new DbUpdateConcurrencyException(
                "Simulated concurrency conflict. " +
                "Check the log to see if OperationReprocessor retried the command 3 times.");

        return todo;
    }

    public virtual async Task Remove(Todos_Remove command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive)
            return;

        var (session, id) = command;
        var user = await auth.GetUser(session, cancellationToken).Require();
        var key = GetTodoKey(user, id);
        var doneKey = GetDoneKey(user, id);
        await store.Remove(session, key, cancellationToken);
        await store.Remove(session, doneKey, cancellationToken);
    }

    // Queries

    public virtual async Task<Todo?> Get(Session session, string id, CancellationToken cancellationToken = default)
    {
        var user = await auth.GetUser(session, cancellationToken).Require();
        var key = GetTodoKey(user, id);
        return await store.Get<Todo>(session, key, cancellationToken);
    }

    public virtual async Task<Todo[]> List(Session session, PageRef<string> pageRef, CancellationToken cancellationToken = default)
    {
        var user = await auth.GetUser(session, cancellationToken).Require();
        var keyPrefix = GetTodoKeyPrefix(user);
        var keySuffixes = await store.ListKeySuffixes(session, keyPrefix, pageRef, cancellationToken);
        var tasks = keySuffixes.Select(suffix => store.Get<Todo>(session, keyPrefix + suffix, cancellationToken).AsTask());
        var todos = await Task.WhenAll(tasks);
        return todos.Where(todo => todo != null).ToArray()!;
    }

    public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
    {
        var user = await auth.GetUser(session, cancellationToken).Require();
        var count = await store.Count(session, GetTodoKeyPrefix(user), cancellationToken);
        var doneCount = await store.Count(session, GetDoneKeyPrefix(user), cancellationToken);
        return new TodoSummary(count, doneCount);
    }

    // Private methods

    private string GetTodoKey(User user, string id)
        => $"{GetTodoKeyPrefix(user)}/{id}";
    private string GetDoneKey(User user, string id)
        => $"{GetDoneKeyPrefix(user)}/{id}";

    private string GetTodoKeyPrefix(User user)
        => $"@user/{user.Id}/todo/items";
    private string GetDoneKeyPrefix(User user)
        => $"@user/{user.Id}/todo/done";
}
