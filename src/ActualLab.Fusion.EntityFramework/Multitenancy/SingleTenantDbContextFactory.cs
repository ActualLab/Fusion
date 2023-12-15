using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Multitenancy;

namespace ActualLab.Fusion.EntityFramework;

public sealed class SingleTenantDbContextFactory<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    TDbContext>(IDbContextFactory<TDbContext> dbContextFactory)
    : IMultitenantDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private IDbContextFactory<TDbContext> DbContextFactory { get; } = dbContextFactory;

    public TDbContext CreateDbContext(Symbol tenantId)
        => tenantId == Tenant.Default
            ? DbContextFactory.CreateDbContext()
            : throw Errors.NonDefaultTenantIsUsedInSingleTenantMode();
}
