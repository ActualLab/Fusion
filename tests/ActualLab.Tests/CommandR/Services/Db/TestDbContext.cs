using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;

namespace ActualLab.Tests.CommandR.Services;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContextBase(options)
{
    public DbSet<User> Users { get; protected set; } = null!;

    // ActualLab.Fusion.EntityFramework.Operations tables
    public DbSet<DbOperation> Operations { get; protected set; } = null!;
    public DbSet<DbOperationEvent> OperationEvents { get; protected set; } = null!;
    public DbSet<DbOperationTimer> OperationTimers { get; protected set; } = null!;
}
