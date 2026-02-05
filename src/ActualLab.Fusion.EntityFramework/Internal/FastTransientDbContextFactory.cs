using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// An <see cref="IDbContextFactory{TDbContext}"/> implementation that creates
/// <see cref="DbContext"/> instances using a provided factory delegate.
/// </summary>
public class FuncDbContextFactory<TDbContext>(Func<TDbContext> factory) : IDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    public TDbContext CreateDbContext()
        => factory.Invoke();
}
