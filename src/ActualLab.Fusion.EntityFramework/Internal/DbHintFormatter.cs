using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Internal;

public interface IDbHintFormatter
{
    void Configure(IServiceCollection services);
    IQueryable<T> Apply<T>(DbSet<T> dbSet, ref MemoryBuffer<DbHint> hints)
        where T : class;
}

public abstract class DbHintFormatter : IDbHintFormatter
{
    protected Dictionary<DbHint, string> DbHintToSql { get; init; } = new();

    public virtual void Configure(IServiceCollection services)
    { }

    public abstract IQueryable<T> Apply<T>(DbSet<T> dbSet, ref MemoryBuffer<DbHint> hints)
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
