using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion.EntityFramework;
using static System.Console;

// ReSharper disable once CheckNamespace
namespace TutorialEF;

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

public static class PartEF
{
    public static async Task Run()
    {
        WriteLine("Part EF: Entity Framework Extensions");
        WriteLine();
        WriteLine("This part covers DbHub, sharding, and DbEntityResolver.");
        WriteLine("See PartEF.md for full documentation.");
    }
}
