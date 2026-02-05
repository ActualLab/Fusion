using System.Text;
using ActualLab.IO;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// Defines the contract for formatting <see cref="DbHint"/> instances into
/// provider-specific SQL and applying them to queries.
/// </summary>
public interface IDbHintFormatter
{
    public void Configure(IServiceCollection services);
    public IQueryable<T> Apply<T>(DbSet<T> dbSet, ref RefArrayPoolBuffer<DbHint> hints)
        where T : class;
}

/// <summary>
/// Abstract base for <see cref="IDbHintFormatter"/> implementations that maps
/// <see cref="DbHint"/> values to SQL strings via a configurable dictionary.
/// </summary>
public abstract class DbHintFormatter : IDbHintFormatter
{
    protected Dictionary<DbHint, string> DbHintToSql { get; init; } = new();

    public virtual void Configure(IServiceCollection services)
    { }

    public abstract IQueryable<T> Apply<T>(DbSet<T> dbSet, ref RefArrayPoolBuffer<DbHint> hints)
        where T : class;

    protected virtual string FormatHint(DbHint hint)
        => hint switch {
            DbCustomHint dbCustomHint => dbCustomHint.Value,
            _ => DbHintToSql.TryGetValue(hint, out var sql)
                ? sql
                : throw Errors.UnsupportedDbHint(hint),
        };

    protected virtual void FormatTableNameTo(StringBuilder sb, string tableName)
    {
        sb.Append('"');
        sb.Append(tableName);
        sb.Append('"');
    }
}
