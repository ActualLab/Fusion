using ActualLab.Fusion.EntityFramework.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ActualLab.Fusion.EntityFramework.Npgsql.Internal;

public class NpgsqlDbHintFormatter : DbHintFormatter
{
    public NpgsqlDbHintFormatter()
        => DbHintToSql = new Dictionary<DbHint, string>() {
            {DbLockingHint.KeyShare, "1:KEY SHARE"},
            {DbLockingHint.Share, "1:SHARE"},
            {DbLockingHint.NoKeyUpdate, "1:NO KEY UPDATE"},
            {DbLockingHint.Update, "1:UPDATE"},
            {DbWaitHint.NoWait, "2:NOWAIT"},
            {DbWaitHint.SkipLocked, "2:SKIP LOCKED"},
        };

    public override void Configure(IServiceCollection services)
    {
        services.AddTransient<NpgsqlHintQuerySqlGeneratorFactory>();
        services.AddScoped<IQuerySqlGeneratorFactory, NpgsqlHintQuerySqlGeneratorFactory>();
    }

    public override IQueryable<T> Apply<T>(DbSet<T> dbSet, ref MemoryBuffer<DbHint> hints)
        where T : class
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append("HINTS:");
        var isFirst = true;
        foreach (var hint in hints) {
            if (!isFirst)
                sb.Append(',');
            sb.Append(FormatHint(hint));
            isFirst = false;
        }
        return dbSet.TagWith(sb.ToStringAndRelease());
    }
}
