using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Authentication.Services;

namespace Tutorial
{
    public partial class AppDbContext
    {
        // Authentication-related tables
        #region Part11_AppDbContext
        public DbSet<DbUser<long>> Users { get; protected set; } = null!;
        public DbSet<DbUserIdentity<long>> UserIdentities { get; protected set; } = null!;
        public DbSet<DbSessionInfo<long>> Sessions { get; protected set; } = null!;
        #endregion
    }
}