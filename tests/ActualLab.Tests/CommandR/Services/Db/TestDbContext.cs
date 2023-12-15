using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;

namespace ActualLab.Tests.CommandR.Services;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContextBase(options)
{
    public DbSet<DbOperation> Operations { get; protected set; } = null!;
    public DbSet<User> Users { get; protected set; } = null!;
}
