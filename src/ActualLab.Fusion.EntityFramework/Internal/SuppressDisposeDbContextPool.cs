using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace ActualLab.Fusion.EntityFramework.Internal;

#pragma warning disable EF1001

/// <summary>
/// An <see cref="IDbContextPool"/> implementation that suppresses disposal of the pooled
/// <see cref="DbContext"/>, allowing it to be reused after being returned to the pool.
/// </summary>
internal sealed class SuppressDisposeDbContextPool(IDbContextPoolable dbContextPoolable) : IDbContextPool
{
#if !NETSTANDARD2_0
    public IDbContextPoolable Rent()
        => dbContextPoolable;

    public void Return(IDbContextPoolable context)
    { }

    public ValueTask ReturnAsync(IDbContextPoolable context, CancellationToken cancellationToken = default)
        => default;
#else
    public DbContext Rent()
        => (DbContext)dbContextPoolable;

    public bool Return(DbContext context)
        => true;
#endif
}
