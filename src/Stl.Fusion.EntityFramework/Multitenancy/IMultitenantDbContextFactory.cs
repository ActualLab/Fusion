using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public interface IMultitenantDbContextFactory<out TDbContext>
    where TDbContext : DbContext
{
    TDbContext CreateDbContext(Symbol tenantId);
}
