using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Npgsql.Internal;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

/// <summary>
/// Extension methods for <see cref="DbContextOptionsBuilder"/> to register the
/// PostgreSQL-specific <see cref="NpgsqlDbHintFormatter"/>.
/// </summary>
public static class DbContextOptionsBuilderExt
{
    public static DbContextOptionsBuilder UseNpgsqlHintFormatter(this DbContextOptionsBuilder dbContext)
        => dbContext.UseHintFormatter<NpgsqlDbHintFormatter>();
}
