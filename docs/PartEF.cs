using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartEF;

// ============================================================================
// PartEF.md snippets: Entity Framework Extensions
// ============================================================================

public class DbTodo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public class DbUser
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options) { }
    public DbSet<DbTodo> Todos => Set<DbTodo>();
    public DbSet<DbUser> Users => Set<DbUser>();
}

public class DbHubSetupExample
{
    #region PartEF_BasicSetup
    // services.AddDbContextServices<AppDbContext>(db => {
    //     // Configure your DbContext
    //     db.AddDbContextFactory(dbContext => {
    //         dbContext.UseNpgsql(connectionString);
    //     });
    // });
    #endregion
}

public record CreateTodoCommand(string Id, string Title) : ICommand<Unit>;

public record Todo
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
}

#region PartEF_DbHubUsage
public class TodoService(DbHub<AppDbContext> dbHub) : IComputeService
{
    [ComputeMethod]
    public virtual async Task<DbTodo[]> GetAll(CancellationToken cancellationToken = default)
    {
        // Create a read-only DbContext (default)
        await using var dbContext = await dbHub.CreateDbContext(cancellationToken);
        return await dbContext.Todos.ToArrayAsync(cancellationToken);
    }

    [CommandHandler]
    public virtual async Task Create(CreateTodoCommand command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = GetAll(default);
            return;
        }

        // Create DbContext for operations (participates in operation scope)
        await using var dbContext = await dbHub.CreateOperationDbContext(cancellationToken);
        dbContext.Todos.Add(new DbTodo { Id = command.Id, Title = command.Title });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
#endregion

public class ShardingConfigExample
{
    #region PartEF_ShardRegistry
    // services.AddDbContextServices<AppDbContext>(db => {
    //     db.AddSharding(sharding => {
    //         // Register multiple shards
    //         sharding.AddShardRegistry("tenant0", "tenant1", "tenant2");
    //
    //         // Or dynamically
    //         var tenants = Enumerable.Range(0, tenantCount).Select(i => $"tenant{i}");
    //         sharding.AddShardRegistry(tenants);
    //     });
    // });
    #endregion
}

#region PartEF_CustomShardResolver
public interface ITenantCommand
{
    string TenantId { get; }
}

// public class TenantShardResolver(IServiceProvider services)
//     : DbShardResolver<AppDbContext>(services)
// {
//     public override string Resolve(object source)
//     {
//         // Custom resolution for tenant-specific commands
//         if (source is ITenantCommand tenantCommand)
//             return tenantCommand.TenantId;
//
//         // Custom resolution for user IDs
//         if (source is UserId userId)
//             return GetShardForUser(userId);
//
//         return base.Resolve(source);
//     }
// }
//
// // Register custom resolver
// db.AddSharding(sharding => {
//     sharding.AddShardResolver<TenantShardResolver>();
// });
#endregion

public class EntityResolverSetupExample
{
    #region PartEF_EntityResolverSetup
    // services.AddDbContextServices<AppDbContext>(db => {
    //     // Simple setup - key extracted from entity's key property
    //     db.AddEntityResolver<string, DbTodo>();
    //
    //     // With options
    //     db.AddEntityResolver<string, DbTodo>(_ => new() {
    //         KeyExtractor = e => e.Id,
    //         BatchSize = 20,
    //         Timeout = TimeSpan.FromSeconds(3),
    //     });
    // });
    #endregion
}

#region PartEF_EntityResolverUsage
public class TodoServiceWithResolver(IDbEntityResolver<string, DbTodo> todoResolver) : IComputeService
{
    [ComputeMethod]
    public virtual async Task<DbTodo?> Get(string id, CancellationToken cancellationToken = default)
    {
        // This call is automatically batched with other concurrent calls
        return await todoResolver.Get(id, cancellationToken);
    }

    [ComputeMethod]
    public virtual async Task<Dictionary<string, DbTodo>> GetMany(string[] ids, CancellationToken cancellationToken = default)
    {
        // Fetch multiple entities efficiently
        return await todoResolver.GetMany(ids, cancellationToken);
    }
}
#endregion

#region PartEF_DbServiceBase
public class TodoServiceBase(IServiceProvider services)
    : DbServiceBase<AppDbContext>(services), IComputeService
{
    // DbHub is available via protected property
    [ComputeMethod]
    public virtual async Task<DbTodo[]> GetAll(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateDbContext(cancellationToken);
        return await dbContext.Todos.ToArrayAsync(cancellationToken);
    }
}
#endregion

public class CompleteSetupExample
{
    #region PartEF_CompleteSetup
    // var builder = WebApplication.CreateBuilder(args);
    //
    // builder.Services.AddDbContextServices<AppDbContext>(db => {
    //     // Configure DbContext factory
    //     db.AddDbContextFactory(dbContext => {
    //         dbContext.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    //     });
    //
    //     // Optional: Configure sharding for multi-tenant
    //     db.AddSharding(sharding => {
    //         var tenants = new[] { "tenant0", "tenant1", "tenant2" };
    //         sharding.AddShardRegistry(tenants);
    //         sharding.AddTransientShardDbContextFactory((c, shard) => {
    //             var connStr = builder.Configuration.GetConnectionString(shard.Value);
    //             return new AppDbContext(
    //                 new DbContextOptionsBuilder<AppDbContext>()
    //                     .UseNpgsql(connStr)
    //                     .Options);
    //         });
    //     });
    //
    //     // Add entity resolvers for efficient batch loading
    //     db.AddEntityResolver<string, DbTodo>();
    //     db.AddEntityResolver<long, DbUser>();
    //
    //     // Add Operations Framework (uses all the above)
    //     db.AddOperations(operations => {
    //         operations.AddNpgsqlOperationLogWatcher();
    //     });
    // });
    //
    // // Register your services
    // builder.Services.AddFusion()
    //     .AddService<TodoService>();
    #endregion
}

public class ShardResolverUsageExample(DbHub<AppDbContext> dbHub, IDbShardResolver<AppDbContext> shardResolver)
{
    public record MyCommand(string TenantId) : ICommand<Unit>;

    #region PartEF_ShardResolverUsage
    public async Task ProcessCommand(MyCommand command, CancellationToken cancellationToken)
    {
        // Resolve shard from the command (e.g., from session or IHasShard)
        var shard = shardResolver.Resolve(command);

        // Create DbContext for the resolved shard
        await using var dbContext = await dbHub.CreateOperationDbContext(shard, cancellationToken);

        // Work with the shard-specific database
        // ...
    }
    #endregion
}

public class SessionBasedShardingExample
{
    #region PartEF_SessionBasedSharding
    // // Set the session tag used for shard resolution
    // DbShardResolver.DefaultSessionShardTag = "tenant";
    //
    // // When creating sessions, include the shard
    // var session = new Session($"session-id").WithTag("tenant", "tenant0");
    #endregion
}

public class PerShardDbContextConfigExample
{
    #region PartEF_PerShardConfig
    // db.AddSharding(sharding => {
    //     sharding.AddShardRegistry("tenant0", "tenant1", "tenant2");
    //
    //     sharding.AddTransientShardDbContextFactory((c, shard) => {
    //         var connectionString = GetConnectionString(shard.Value);
    //         return new AppDbContext(
    //             new DbContextOptionsBuilder<AppDbContext>()
    //                 .UseNpgsql(connectionString)
    //                 .Options);
    //     });
    // });
    #endregion
}

public class QueryTransformationExample
{
    #region PartEF_QueryTransformation
    // db.AddEntityResolver<string, DbTodo>(_ => new() {
    //     // Only load active todos, include related data
    //     QueryTransformer = q => q
    //         .Where(t => !t.IsDeleted)
    //         .Include(t => t.Tags),
    // });
    #endregion
}
