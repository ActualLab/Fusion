using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartEF;

// Sample DbContext for PartEF
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContextBase(options)
{
    public DbSet<DbTodo> Todos => Set<DbTodo>();
    public DbSet<DbUser> Users => Set<DbUser>();
}

public class DbTodo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsDeleted { get; set; }
}

public class DbUser
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class PartEF : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Entity Framework Extensions");
        WriteLine("This part covers DbHub, sharding, and DbEntityResolver.");
        WriteLine("See PartEF.md for full documentation.");
        await Task.CompletedTask;
    }
}
