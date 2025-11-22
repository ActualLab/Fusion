using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;

namespace Docs
{
    public class AppDbContext(DbContextOptions options) : DbContextBase(options)
    {
        // ActualLab.Fusion.EntityFramework.Operations tables
        #region Part10_DbSet
        public DbSet<DbOperation> Operations { get; protected set; } = null!;
        #endregion
    }
}