using ActualLab.Fusion.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Extensions;

public class FusionExtensionsAuditRegressionTest
{
    [Fact]
    public async Task QueryablePaginationShouldTranslateToSql()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AuditDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Items.AddRange(new AuditItem { Key = "a" }, new AuditItem { Key = "b" });
        await dbContext.SaveChangesAsync();

        var query = dbContext.Items.OrderByAndTakePage(x => x.Key, new PageRef<string>(10, "a"));
        var action = () => query.Select(x => x.Key).ToArrayAsync();

        (await action.Should().NotThrowAsync()).Which.Should().Equal("b");
    }

    private sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
    {
        public DbSet<AuditItem> Items => Set<AuditItem>();
    }

    private sealed class AuditItem
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
    }
}
