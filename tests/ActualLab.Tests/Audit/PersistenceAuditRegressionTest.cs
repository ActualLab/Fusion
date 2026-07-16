using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Redis;
using ActualLab.Tests.CommandR;
using ActualLab.Tests.CommandR.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ActualLab.Tests.Audit;

public class PersistenceAuditRegressionTest : CommandRTestBase
{
    public PersistenceAuditRegressionTest(ITestOutputHelper @out) : base(@out)
        => UseDbContext = true;

    [Fact]
    public void SaveChangesGuardShouldBeIdempotent()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        using var dbContext = factory.CreateDbContext();
        dbContext.EnableSaveChanges(false);
        dbContext.EnableSaveChanges(false);
        var save = () => dbContext.SaveChanges();

        save.Should().Throw<InvalidOperationException>();

        dbContext.EnableSaveChanges(true);
        dbContext.EnableSaveChanges(true);

        save.Should().NotThrow();
    }

    [Fact]
    public void SaveChangesGuardShouldSurviveContextPooling()
    {
        var services = new ServiceCollection();
        services.AddPooledDbContextFactory<TestDbContext>(
            options => options.UseInMemoryDatabase(nameof(SaveChangesGuardShouldSurviveContextPooling)),
            poolSize: 1);
        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var firstDbContext = factory.CreateDbContext();
        firstDbContext.EnableSaveChanges(false);
        firstDbContext.Dispose();

        using var secondDbContext = factory.CreateDbContext();
        secondDbContext.Should().BeSameAs(firstDbContext);
        secondDbContext.EnableSaveChanges(false);
        var save = () => secondDbContext.SaveChanges();

        save.Should().Throw<InvalidOperationException>();

        secondDbContext.EnableSaveChanges(true);
        save.Should().NotThrow();
    }

    [Fact]
    public void OperationsShouldRegisterTheEventResolverWithItsStringKey()
    {
        var services = CreateServices();

        services.GetService<IDbEntityResolver<string, DbEvent>>().Should().NotBeNull();
    }

    [Fact]
    public void WaitHintsShouldHaveTheWaitHintRuntimeType()
    {
        DbWaitHint.NoWait.Should().BeOfType<DbWaitHint>();
        DbWaitHint.SkipLocked.Should().BeOfType<DbWaitHint>();
    }

    [Fact]
    public void TypedRedisDatabasesShouldRetainTheirOwnConnectors()
    {
        var services = new ServiceCollection();
        var multiplexerSource = TaskCompletionSourceExt.New<IConnectionMultiplexer>();
        services.AddRedisDb<FirstRedisScope>(() => multiplexerSource.Task);
        services.AddRedisDb<SecondRedisScope>(() => multiplexerSource.Task);
        using var serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.GetRequiredService<RedisDb<FirstRedisScope>>();
        var second = serviceProvider.GetRequiredService<RedisDb<SecondRedisScope>>();

        first.Connector.Should().NotBeSameAs(second.Connector);
    }

    private sealed class FirstRedisScope;
    private sealed class SecondRedisScope;
}
