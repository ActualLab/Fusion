using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ActualLab.Fusion.EntityFramework.Internal;

namespace ActualLab.Fusion.EntityFramework;

public static class DbSetExt
{
    public static DbContext GetDbContext<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.GetInfrastructure().GetRequiredService<ICurrentDbContext>().Context;

    public static string GetTableName<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
    {
        var dbContext = dbSet.GetDbContext();
        var model = dbContext.Model;
        var entityTypes = model.GetEntityTypes();
        var entityType = entityTypes.Single(t => t.ClrType == typeof(TEntity));
        var tableNameAnnotation = entityType.GetAnnotation("Relational:TableName");
        var tableName = tableNameAnnotation.Value!.ToString();
        return tableName!;
    }

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, DbHint hint)
        where TEntity: class
    {
        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.Add(hint);
            return hintFormatter.Apply(dbSet, ref mHints);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, DbHint hint1, DbHint hint2)
        where TEntity: class
    {
        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.Add(hint1);
            mHints.Add(hint2);
            return hintFormatter.Apply(dbSet, ref mHints);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(
        this DbSet<TEntity> dbSet, DbHint hint1, DbHint hint2, DbHint hint3)
        where TEntity: class
    {
        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.Add(hint1);
            mHints.Add(hint2);
            mHints.Add(hint3);
            return hintFormatter.Apply(dbSet, ref mHints);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] hints)
        where TEntity: class
    {
        if (hints.Length == 0)
            return dbSet;

        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.AddRange(hints);
            return hintFormatter.Apply(dbSet, ref mHints);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> ForShare<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.Share);

    public static IQueryable<TEntity> ForKeyShare<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.KeyShare);

    public static IQueryable<TEntity> ForUpdate<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.Update);

    public static IQueryable<TEntity> ForNoKeyUpdate<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.NoKeyUpdate);

    public static IQueryable<TEntity> ForUpdateSkipLocked<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.Update, DbWaitHint.SkipLocked);

    public static IQueryable<TEntity> ForNoKeyUpdateSkipLocked<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.NoKeyUpdate, DbWaitHint.SkipLocked);
}
