using ActualLab.Fusion.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Services.Db;

namespace Samples.TodoApp.Services;

public class TodoBackend(IServiceProvider services) : DbServiceBase<AppDbContext>(services), ITodoBackend
{
    private IDbEntityResolver<string, DbTodo> DbTodoResolver { get; } = services.DbEntityResolver<string, DbTodo>();

    // Commands

    public virtual async Task<TodoItem> AddOrUpdate(TodoBackend_AddOrUpdate command, CancellationToken cancellationToken = default)
    {
        var (folder, item) = command;
        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            // Invalidation logic
            var isNew = context.Operation.Items.Get<bool>("New");
            var isDoneChanged = context.Operation.Items.Get<bool>("IsDoneChanged");
            _ = Get(folder, item.Id, default);
            if (isNew)
                _ = PseudoListIds(folder);
            if (isNew || isDoneChanged)
                _ = GetSummary(folder, default);
            return null!;
        }

        var tenant = folder.GetTenant();
        var dbContext = await DbHub.CreateOperationDbContext(tenant, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        // These "throw"-s are to show how error handling / reprocessing works
        if (item.Title.Contains("@"))
            throw new InvalidOperationException("Todo title can't contain '@' symbol.");
        if (item.Title.Contains("#"))
            throw new DbUpdateConcurrencyException(
                "Simulated concurrency conflict. " +
                "Check the log to see if OperationReprocessor retried the command 3 times.");

        if (item.Id == Ulid.Empty) {
            item = item with { Id = Ulid.NewUlid() };
            dbContext.Add(new DbTodo(folder, item));
            // A tag for Invalidation.IsActive block indicating an item was added
            CommandContext.GetCurrent().Operation.Items.Set("New", true);
        }
        else {
            var key = DbTodo.ComposeKey(folder, item.Id);
            var dbItem = await dbContext.Todos
                .SingleAsync(x => x.Key == key, cancellationToken)
                .ConfigureAwait(false);
            if (dbItem.IsDone != item.IsDone) {
                // A tag for Invalidation.IsActive block indicating IsDone property was changed
                CommandContext.GetCurrent().Operation.Items.Set("IsDoneChanged", true);
            }
            dbItem.UpdateFrom(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item;
    }

    public virtual async Task Remove(TodoBackend_Remove command, CancellationToken cancellationToken = default)
    {
        var (folder, id) = command;
        if (Invalidation.IsActive) {
            // Invalidation logic
            _ = Get(folder, id, default);
            _ = GetSummary(folder, default);
            _ = PseudoListIds(folder);
            return;
        }

        var tenant = folder.GetTenant();
        var dbContext = await DbHub.CreateOperationDbContext(tenant, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbTodo = await dbContext
            .FindAsync<DbTodo>(DbKey.Compose(DbTodo.ComposeKey(folder, id)), cancellationToken)
            .ConfigureAwait(false);
        if (dbTodo != null) {
            dbContext.Remove(dbTodo);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Queries

    public virtual async Task<TodoItem?> Get(string folder, Ulid id, CancellationToken cancellationToken = default)
    {
        var tenant = folder.GetTenant();
        var dbTodo = await DbTodoResolver
            .Get(tenant, DbTodo.ComposeKey(folder, id), cancellationToken)
            .ConfigureAwait(false);
        return dbTodo?.ToModel();
    }

    public virtual async Task<Ulid[]> ListIds(string folder, int limit, CancellationToken cancellationToken = default)
    {
        await PseudoListIds(folder).ConfigureAwait(false);

        var tenant = folder.GetTenant();
        var dbContext = await DbHub.CreateDbContext(tenant, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        var prefix = $"{folder}/";
        var keys = await dbContext.Todos
            .Where(x => x.Key.StartsWith(prefix))
            .OrderBy(x => x.Key) // We want 100% stable order here
            .Select(x => x.Key)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return keys.Select(id => DbTodo.SplitKey(id).Id).ToArray();
    }

    public virtual async Task<TodoSummary> GetSummary(string folder, CancellationToken cancellationToken = default)
    {
        var tenant = folder.GetTenant();
        var dbContext = await DbHub.CreateDbContext(tenant, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);

        var prefix = $"{folder}/";
        var count = await dbContext.Todos
            .CountAsync(x => x.Key.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);
        var doneCount = await dbContext.Todos
            .CountAsync(x => x.Key.StartsWith(prefix) && x.IsDone, cancellationToken)
            .ConfigureAwait(false);
        return new TodoSummary(count, doneCount);
    }

    // Pseudo queries

    // This is a "pseudo access" method that's called solely to become a dependency.
    // When it gets invalidated, it also invalidates all ListIds(folder, <any_limit>) at once.
    // See the places it's called from to understand how it works.
    [ComputeMethod]
    protected virtual Task<Unit> PseudoListIds(string folder)
        => TaskExt.UnitTask;
}
