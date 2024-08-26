using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Redis;
using ActualLab.IO;
using Microsoft.EntityFrameworkCore;
using Samples.HelloCart.V2;

namespace Samples.HelloCart;

public static class AppDb
{
    public static void Configure(IServiceCollection services)
    {
        // IDbContextFactory
        services.AddPooledDbContextFactory<AppDbContext>(db => {
            if (AppSettings.Db.UsePostgreSql) {
                var connectionString =
                    "Server=localhost;Database=fusion_hellocart;Port=5432;User Id=postgres;Password=postgres";
                db.UseNpgsql(connectionString, npgsql => {
                    npgsql.EnableRetryOnFailure(0);
                });
                db.UseNpgsqlHintFormatter();
            }
            else {
                var appTempDir = FilePath.GetApplicationTempDirectory("", true);
                var dbPath = appTempDir & "HelloCart_v1.db";
                db.UseSqlite($"Data Source={dbPath}");
            }
            db.EnableSensitiveDataLogging();
        });

        // Related services
        services.AddDbContextServices<AppDbContext>(db => {
            db.AddOperations(operations => {
                if (!AppSettings.Db.UseOperationLogWatchers)
                    return;

                if (AppSettings.Db.UseRedisOperationLogWatchers) {
                    db.AddRedisDb("localhost", "Fusion.Samples.HelloCart");
                    operations.AddRedisOperationLogWatcher();
                }
                else if (AppSettings.Db.UsePostgreSql)
                    operations.AddNpgsqlOperationLogWatcher();
                else
                    operations.AddFileSystemOperationLogWatcher();
            });
            db.AddEntityResolver<string, DbProduct>();
            db.AddEntityResolver<string, DbCart>(_ => new() {
                // Cart is always loaded together with items
                QueryTransformer = carts => carts.Include(c => c.Items),
            });
        });

        // Operation reprocessor
        if (AppSettings.Db.UseOperationReprocessor)
            services.AddFusion().AddOperationReprocessor();
    }
}
