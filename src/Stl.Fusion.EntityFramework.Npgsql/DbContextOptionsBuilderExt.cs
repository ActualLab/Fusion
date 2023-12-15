using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Npgsql.Internal;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

public static class DbContextOptionsBuilderExt
{
    public static DbContextOptionsBuilder UseNpgsqlHintFormatter(this DbContextOptionsBuilder dbContext)
        => dbContext.UseHintFormatter<NpgsqlDbHintFormatter>();
}
