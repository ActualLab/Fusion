using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Tests.DbModel;

namespace ActualLab.Fusion.Tests;

public class DbContextPoolTest : FusionTestBase
{
    private const int DrainCount = 32;
    private const int PoisonCount = 8;

    public DbContextPoolTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.PostgreSql;

    [Fact]
    public async Task PoisonedDbContextDoesNotBreakOperations()
    {
        if (MustSkip())
            return;

        var contextFactory = Services.GetRequiredService<IShardDbContextFactory<TestDbContext>>();
        var drainedDbContexts = new List<TestDbContext>();
        try {
            for (var i = 0; i < DrainCount; i++)
                drainedDbContexts.Add(await contextFactory.CreateDbContextAsync(DbShard.Single));

            // An unbalanced Open leaves the same state a query cancelled mid-flight does: EF's
            // pool-return path closes the connection, but its open count stays non-zero
            for (var i = 0; i < PoisonCount; i++)
                await drainedDbContexts[i].GetService<IRelationalConnection>().OpenAsync(CancellationToken.None);
            for (var i = 0; i < PoisonCount; i++)
                await drainedDbContexts[i].DisposeAsync();
            drainedDbContexts.RemoveRange(0, PoisonCount);

            var keyValueStore = Services.GetRequiredService<IKeyValueStore>();
            for (var i = 0; i < PoisonCount; i++) {
                var key = $"poisoned/{i}";
                await keyValueStore.Set(DbShard.Single, key, key);
                (await keyValueStore.Get(DbShard.Single, key)).Should().Be(key);
            }
        }
        finally {
            foreach (var dbContext in drainedDbContexts)
                await dbContext.DisposeAsync();
        }
    }
}
