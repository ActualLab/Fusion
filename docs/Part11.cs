using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.Authentication.Services;

// ReSharper disable once CheckNamespace
namespace Tutorial11;

public class AppDbContext
{
    // Authentication-related tables
    #region Part11_AppDbContext
    public DbSet<DbUser<long>> Users { get; protected set; } = null!;
    public DbSet<DbUserIdentity<long>> UserIdentities { get; protected set; } = null!;
    public DbSet<DbSessionInfo<long>> Sessions { get; protected set; } = null!;
    #endregion
}
