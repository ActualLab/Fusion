using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Redis;
using ActualLab.IO;
using Microsoft.EntityFrameworkCore;
using Samples.HelloCart.V2;

namespace Samples.HelloCart;

public static class AppDb
{
    public static bool UsePostgreSql { get; set; } = true;
    public static bool UseOperationLogWatchers { get; set; } = true;
    public static bool UseRedisOperationLogWatchers { get; set; } = true;

    public static void Configure(IServiceCollection services)
    {
        // Add AppDbContext & related services
        services.AddPooledDbContextFactory<AppDbContext>(db => {
            if (UsePostgreSql) {
                var connectionString =
                    "Server=localhost;Database=fusion_hellocart;Port=5432;User Id=postgres;Password=postgres";
                db.UseNpgsql(connectionString, npgsql => {
                    npgsql.EnableRetryOnFailure(0);
                });
                db.UseNpgsqlHintFormatter();
            }
            else {
                var appTempDir = FilePath.GetApplicationTempDirectory("", true);
                var dbPath = appTempDir & "HelloCart.db";
                db.UseSqlite($"Data Source={dbPath}");
            }
            db.EnableSensitiveDataLogging();
        });
        services.AddDbContextServices<AppDbContext>(db => {
            db.AddOperations(operations => {
                if (!UseOperationLogWatchers)
                    return;

                if (UseRedisOperationLogWatchers) {
                    db.AddRedisDb("localhost", "Fusion.Samples.HelloCart");
                    operations.AddRedisOperationLogWatcher();
                }
                else if (UsePostgreSql)
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
    }
}
