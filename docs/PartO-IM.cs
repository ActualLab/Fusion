using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Interception;
using ActualLab.CommandR.Operations;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedParameter.Local

// ReSharper disable once CheckNamespace
namespace Docs.PartOIM;

// ============================================================================
// PartO-IM.md snippets: Invalidation Modes
// ============================================================================

public class AppDbContext(DbContextOptions options) : DbContextBase(options)
{
    public DbSet<DbOperation> Operations => Set<DbOperation>();
    public DbSet<DbEvent> Events => Set<DbEvent>();
}

public record Contact(string OwnerId, string Id, string Name);

public record Contacts_Change(string OwnerId, string Id, string Name) : ICommand<Unit>;

public record Tags_Add(string Tag) : ICommand<Unit>;

public record Prices_Set(string Symbol, decimal Price) : ICommand<Unit>;

#region PartOIM_Local
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class Contacts(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    [CommandHandler]
    public virtual async Task OnChange(Contacts_Change command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        // ... mutate ...
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidation.Defer(() => {
            _ = Get(command.OwnerId, command.Id, default);
            _ = ListIds(command.OwnerId, default);
        });
    }

    [ComputeMethod]
    public virtual Task<Contact?> Get(string ownerId, string id, CancellationToken ct)
        => Task.FromResult<Contact?>(null);
    [ComputeMethod]
    public virtual Task<string[]> ListIds(string ownerId, CancellationToken ct)
        => Task.FromResult(Array.Empty<string>());
}
#endregion

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class Tags(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartOIM_Conditional
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task OnAdd(Tags_Add command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        var affectedOwnerIds = new[] { "o1", "o2" };
        await dbContext.SaveChangesAsync(cancellationToken);

        // Ordinary control flow - the condition is evaluated once, right here
        Invalidation.Defer(() => {
            foreach (var ownerId in affectedOwnerIds)
                _ = ListIds(ownerId, default);
            if (affectedOwnerIds.Length > 1)
                _ = Count(default);
        });
    }
    #endregion

    [ComputeMethod]
    public virtual Task<string[]> ListIds(string ownerId, CancellationToken ct)
        => Task.FromResult(Array.Empty<string>());
    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken ct)
        => Task.FromResult(0);
}

#region PartOIM_Replicated
// Every host caches the whole price table, so every host has to invalidate its own copy
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public class Prices(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    [CommandHandler]
    public virtual async Task OnSet(Prices_Set command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        // ... mutate ...
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidation.Defer(() => _ = Get(command.Symbol, default));
    }

    [ComputeMethod]
    public virtual Task<decimal> Get(string symbol, CancellationToken ct)
        => Task.FromResult(0m);
}
#endregion

#region PartOIM_Distributed
// The contact lives on exactly one host, so invalidating it on all of them is wasted work
[DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
public class ShardedContacts(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    [CommandHandler]
    public virtual async Task OnChange(Contacts_Change command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        // ... mutate ...
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidation.Defer(() => _ = Get(command.OwnerId, command.Id, default));
    }

    [ComputeMethod]
    public virtual Task<Contact?> Get(string ownerId, string id, CancellationToken ct)
        => Task.FromResult<Contact?>(null);
}
#endregion

public interface IInventory : IComputeService
{
    #region PartOIM_InterfaceDeclaration
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
    Task OnReserve(Inventory_Reserve command, CancellationToken cancellationToken = default);
    #endregion
}

public record Inventory_Reserve(string Sku, int Count) : ICommand<Unit>;

public static class PartOIMConfiguration
{
    public static void ConfigureCustomResolver(IServiceCollection services)
    {
        #region PartOIM_CustomResolver
        // The resolver is an ordinary singleton, so registering your own after AddFusion()
        // replaces it
        services.AddFusion();
        services.AddSingleton<DeferredInvalidationModeResolver>(
            c => new TenantInvalidationModeResolver(
                c.GetRequiredService<ServiceTypeResolver>()));
        #endregion
    }
}

#region PartOIM_ResolverBody
public class TenantInvalidationModeResolver(ServiceTypeResolver serviceTypeResolver)
    : DeferredInvalidationModeResolver(serviceTypeResolver)
{
    public override DeferredInvalidationMode Resolve(IMethodCommandHandler handler)
        => handler.GetHandlerServiceType().Namespace?.StartsWith("MyApp.Sharded", StringComparison.Ordinal) == true
            ? DeferredInvalidationMode.Distributed
            : base.Resolve(handler);
}
#endregion
