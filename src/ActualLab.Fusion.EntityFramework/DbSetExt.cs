using System.Buffers;
using ActualLab.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ActualLab.Fusion.EntityFramework.Internal;

namespace ActualLab.Fusion.EntityFramework;

public static class DbSetExt
{
    private static readonly ArrayPool<DbHint> SharedDbHintPool = ArrayPool<DbHint>.Shared;

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
        if (hintFormatter is null)
            return dbSet;

        var hints = new RefArrayPoolBuffer<DbHint>(SharedDbHintPool, 8, mustClear: false);
        try {
            hints.Add(hint);
            return hintFormatter.Apply(dbSet, ref hints);
        }
        finally {
            hints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, DbHint hint1, DbHint hint2)
        where TEntity: class
    {
        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter is null)
            return dbSet;

        var hints = new RefArrayPoolBuffer<DbHint>(SharedDbHintPool, 8, mustClear: false);
        try {
            hints.Add(hint1);
            hints.Add(hint2);
            return hintFormatter.Apply(dbSet, ref hints);
        }
        finally {
            hints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(
        this DbSet<TEntity> dbSet, DbHint hint1, DbHint hint2, DbHint hint3)
        where TEntity: class
    {
        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter is null)
            return dbSet;

        var hints = new RefArrayPoolBuffer<DbHint>(SharedDbHintPool, 8, mustClear: false);
        try {
            hints.Add(hint1);
            hints.Add(hint2);
            hints.Add(hint3);
            return hintFormatter.Apply(dbSet, ref hints);
        }
        finally {
            hints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] hints)
        where TEntity: class
    {
        if (hints.Length == 0)
            return dbSet;

        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter is null)
            return dbSet;

        var buffer = new RefArrayPoolBuffer<DbHint>(SharedDbHintPool, hints.Length, mustClear: false);
        try {
            buffer.AddRange(hints);
            return hintFormatter.Apply(dbSet, ref buffer);
        }
        finally {
            buffer.Release();
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
