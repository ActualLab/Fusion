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

    public static IQueryable<TEntity> WithHints<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] hints)
        where TEntity: class
    {
        if (hints.Length == 0)
            return dbSet;

        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var tableName = dbSet.GetTableName();
        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.AddSpan(hints.AsSpan());
            if (mHints.Count == 0)
                return dbSet;
            var sql = hintFormatter.FormatSelectSql(tableName, ref mHints);
            return dbSet.FromSqlRaw(sql);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> WithHints<TEntity>(
        this DbSet<TEntity> dbSet,
        DbHint primaryHint,
        params DbHint[] hints)
        where TEntity: class
    {
        if (hints.Length == 0)
            return dbSet;

        var hintFormatter = dbSet.GetInfrastructure().GetService<IDbHintFormatter>();
        if (hintFormatter == null)
            return dbSet;

        var tableName = dbSet.GetTableName();
        var mHints = MemoryBuffer<DbHint>.Lease(false);
        try {
            mHints.Add(primaryHint);
            mHints.AddSpan(hints.AsSpan());
            if (mHints.Count == 0)
                return dbSet;
            var sql = hintFormatter.FormatSelectSql(tableName, ref mHints);
            return dbSet.FromSqlRaw(sql);
        }
        finally {
            mHints.Release();
        }
    }

    public static IQueryable<TEntity> ForShare<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] otherHints)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.Share, otherHints);

    public static IQueryable<TEntity> ForKeyShare<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] otherHints)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.KeyShare, otherHints);

    public static IQueryable<TEntity> ForUpdate<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] otherHints)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.Update, otherHints);

    public static IQueryable<TEntity> ForNoKeyUpdate<TEntity>(this DbSet<TEntity> dbSet, params DbHint[] otherHints)
        where TEntity: class
        => dbSet.WithHints(DbLockingHint.NoKeyUpdate, otherHints);
}
