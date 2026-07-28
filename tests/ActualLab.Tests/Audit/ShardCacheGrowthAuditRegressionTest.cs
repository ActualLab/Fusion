using System.Reflection;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Tests.CommandR.Services;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Tests.Audit;

public class ShardCacheGrowthAuditRegressionTest
{
    [Fact]
    public void DbShardResolverShouldRejectUnregisteredShards()
    {
        using var services = CreateServices();
        var shardResolver = services.GetRequiredService<IDbShardResolver<TestDbContext>>();

        shardResolver.Resolve(new Session("session01&s=a")).Should().Be("a");

        var resolveUnregistered = () => shardResolver.Resolve(new Session("session01&s=zzz"));
        resolveUnregistered.Should().Throw<InvalidOperationException>();

        var resolveTemplate = () => shardResolver.Resolve(new Session($"session01&s={DbShard.Template}"));
        resolveTemplate.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ShardDbContextFactoryShouldNotCacheUnregisteredShards()
    {
        using var services = CreateServices();
        var contextFactory = services.GetRequiredService<IShardDbContextFactory<TestDbContext>>();

        for (var i = 0; i < 10; i++) {
            var createDbContext = () => contextFactory.CreateDbContext("zzz" + i).Dispose();
            createDbContext.Should().Throw<InvalidOperationException>();
        }
        GetCacheSize(contextFactory, "_factories").Should().Be(0);

        contextFactory.CreateDbContext("a").Dispose();
        GetCacheSize(contextFactory, "_factories").Should().Be(1);
    }

    [Fact]
    public async Task DbEntityResolverShouldNotCacheUnregisteredShards()
    {
        await using var services = CreateServices();
        var entityResolver = services.GetRequiredService<IDbEntityResolver<string, User>>();

        for (var i = 0; i < 10; i++) {
            var get = async () => await entityResolver.Get("zzz" + i, "some-id");
            await get.Should().ThrowAsync<InvalidOperationException>();
        }
        GetCacheSize(entityResolver, "_batchProcessors").Should().Be(0);

        (await entityResolver.Get("a", "some-id")).Should().BeNull();
        GetCacheSize(entityResolver, "_batchProcessors").Should().Be(1);
    }

    // Private methods

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => {
            db.AddSharding(sharding => sharding
                .AddShardRegistry("a", "b")
                .AddTransientShardDbContextFactory((_, shard, dbOptions)
                    => dbOptions.UseInMemoryDatabase($"{nameof(ShardCacheGrowthAuditRegressionTest)}-{shard}")));
            db.AddEntityResolver<string, User>();
        });
        return services.BuildServiceProvider();
    }

    private static int GetCacheSize(object owner, string fieldName)
    {
        var field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return ((ICollection)field!.GetValue(owner)!).Count;
    }
}
