using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Abstract base for database services that have access to an <see cref="IDbHub"/>
/// and common infrastructure such as clocks, commander, and logging.
/// </summary>
public abstract class DbServiceBase(IDbHub dbHub)
{
    protected IServiceProvider Services { get; init; } = dbHub.Services;
    protected IDbHub DbHub { get; } = dbHub;
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}

/// <summary>
/// Abstract base for database services scoped to a specific <typeparamref name="TDbContext"/>,
/// with lazy access to <see cref="DbHub{TDbContext}"/> and common infrastructure.
/// </summary>
public abstract class DbServiceBase<TDbContext>(IServiceProvider services)
    where TDbContext : DbContext
{
    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => field ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}
