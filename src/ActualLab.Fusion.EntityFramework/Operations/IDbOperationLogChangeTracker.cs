using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public interface IDbOperationLogChangeTracker<TDbContext>
    where TDbContext : DbContext
{
    Task WaitForChanges(Symbol tenantId, CancellationToken cancellationToken = default);
}
