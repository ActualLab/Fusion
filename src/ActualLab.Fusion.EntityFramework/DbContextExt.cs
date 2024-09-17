using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using ActualLab.Fusion.EntityFramework.Internal;

namespace ActualLab.Fusion.EntityFramework;

#pragma warning disable EF1001

public static class DbContextExt
{
#if !NETSTANDARD2_0
    private static readonly EventHandler<SavingChangesEventArgs> FailOnSaveChanges =
        (sender, args) => throw Errors.DbContextIsReadOnly();
#endif

    public static IQueryable<TDbEntity> Set<TDbEntity>(this DbContext dbContext, DbHint hint)
        where TDbEntity : class
        => dbContext.Set<TDbEntity>().WithHints(hint);

    public static IQueryable<TDbEntity> Set<TDbEntity>(this DbContext dbContext, DbHint hint1, DbHint hint2)
        where TDbEntity : class
        => dbContext.Set<TDbEntity>().WithHints(hint1, hint2);

    public static IQueryable<TDbEntity> Set<TDbEntity>(this DbContext dbContext, DbHint hint1, DbHint hint2, DbHint hint3)
        where TDbEntity : class
        => dbContext.Set<TDbEntity>().WithHints(hint1, hint2, hint3);

    public static IQueryable<TDbEntity> Set<TDbEntity>(this DbContext dbContext, params DbHint[] hints)
        where TDbEntity : class
        => dbContext.Set<TDbEntity>().WithHints(hints);

    public static TDbContext ReadWrite<TDbContext>(this TDbContext dbContext, bool allowWrites = true)
        where TDbContext : DbContext
        => dbContext
            .EnableChangeTracking(allowWrites)
            .EnableSaveChanges(allowWrites);

    public static TDbContext ReadWrite<TDbContext>(this TDbContext dbContext, bool? allowWrites)
        where TDbContext : DbContext
        => allowWrites is { } vAllowWrites
            ? dbContext.ReadWrite(vAllowWrites)
            : dbContext;

    public static TDbContext EnableChangeTracking<TDbContext>(this TDbContext dbContext, bool mustEnable)
        where TDbContext : DbContext
    {
        var ct = dbContext.ChangeTracker;
        ct.LazyLoadingEnabled = false;
        if (mustEnable) {
            ct.AutoDetectChangesEnabled = true;
            ct.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        }
        else {
            ct.AutoDetectChangesEnabled = false;
            ct.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
        return dbContext;
    }

    public static TDbContext EnableSaveChanges<TDbContext>(this TDbContext dbContext, bool mustEnable)
        where TDbContext : DbContext
    {
#if !NETSTANDARD2_0
        if (mustEnable)
            dbContext.SavingChanges -= FailOnSaveChanges;
        else
            dbContext.SavingChanges += FailOnSaveChanges;
#else
        // Do nothing. DbContext has no SavingChanges event in NETSTANDARD2_0
#endif
        return dbContext;
    }

    public static TDbContext SuppressExecutionStrategy<TDbContext>(this TDbContext dbContext)
        where TDbContext : DbContext
    {
        ExecutionStrategyExt.Suspend(dbContext);
        return dbContext;
    }

    public static TDbContext SuppressDispose<TDbContext>(this TDbContext dbContext)
        where TDbContext : DbContext
    {
        var dbContextPoolable = (IDbContextPoolable)dbContext;
        dbContextPoolable.SnapshotConfiguration();
        var pool = new SuppressDisposeDbContextPool(dbContextPoolable);
#if !NETSTANDARD2_0
        dbContextPoolable.SetLease(new DbContextLease(pool, true));
#else
        dbContextPoolable.SetPool(pool);
#endif
        return dbContext;
    }
}
