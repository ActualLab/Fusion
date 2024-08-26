using ActualLab.Fusion.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Services.Db;

namespace Samples.TodoApp.Services;

public class TodoBackend(IServiceProvider services) : DbServiceBase<AppDbContext>(services), ITodoBackend
{
    private IDbEntityResolver<string, DbTodo> DbTodoResolver { get; } = services.DbEntityResolver<string, DbTodo>();

    // Commands

    public virtual async Task<Todo> AddOrUpdate(TodoBackend_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (folder, todo) = command;
        if (Invalidation.IsActive) {
            _ = Get(folder, todo.Id, default);
            _ = PseudoAccessFolder(folder);
            return null!;
        }

        var tenant = folder.GetTenant();
        await using var dbContext = await DbHub.CreateCommandDbContext(tenant, cancellationToken);

        // These "throw"-s are to show how error handling / reprocessing works
        if (todo.Title.Contains("@"))
            throw new InvalidOperationException("Todo title can't contain '@' symbol.");
        if (todo.Title.Contains("#"))
            throw new DbUpdateConcurrencyException(
                "Simulated concurrency conflict. " +
                "Check the log to see if OperationReprocessor retried the command 3 times.");

        if (todo.Id == Ulid.Empty) {
            todo = todo with { Id = Ulid.NewUlid() };
            dbContext.Add(DbTodo.FromModel(folder, todo));
        }
        else
            dbContext.Update(DbTodo.FromModel(folder, todo));

        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }

    public virtual async Task Remove(TodoBackend_Remove command, CancellationToken cancellationToken = default)
    {
        var (folder, id) = command;
        if (Invalidation.IsActive) {
            _ = Get(folder, id, default);
            _ = PseudoAccessFolder(folder);
            return;
        }

        var tenant = folder.GetTenant();
        await using var dbContext = await DbHub.CreateCommandDbContext(tenant, cancellationToken);

        var dbTodo = await dbContext.FindAsync<DbTodo>(DbKey.Compose(DbTodo.ComposeKey(folder, id)), cancellationToken);
        if (dbTodo != null) {
            dbContext.Remove(dbTodo);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // Queries

    public virtual async Task<Todo?> Get(string folder, Ulid id, CancellationToken cancellationToken = default)
    {
        var tenant = folder.GetTenant();
        var dbTodo = await DbTodoResolver.Get(tenant, DbTodo.ComposeKey(folder, id), cancellationToken);
        return dbTodo?.ToModel();
    }

    public virtual async Task<Ulid[]> ListIds(string folder, int limit, CancellationToken cancellationToken = default)
    {
        await PseudoAccessFolder(folder);

        var tenant = folder.GetTenant();
        await using var dbContext = await DbHub.CreateDbContext(tenant, cancellationToken);

        var keys = await dbContext.Todos
            .OrderBy(x => x.Key) // We want 100% stable order here
            .Select(x => x.Key)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return keys.Select(id => DbTodo.SplitKey(id).Id).ToArray();
    }

    public virtual async Task<TodoSummary> GetSummary(string folder, CancellationToken cancellationToken = default)
    {
        await PseudoAccessFolder(folder);

        var tenant = folder.GetTenant();
        await using var dbContext = await DbHub.CreateDbContext(tenant, cancellationToken);
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var prefix = $"{folder}/";
        var count = await dbContext.Todos.CountAsync(x => x.Key.StartsWith(prefix), cancellationToken);
        var doneCount = await dbContext.Todos.CountAsync(x => x.Key.StartsWith(prefix) && x.IsDone, cancellationToken);
        return new TodoSummary(count, doneCount);
    }

    // Pseudo queries

    [ComputeMethod]
    protected virtual Task<Unit> PseudoAccessFolder(string folder)
        => TaskExt.UnitTask;
}
