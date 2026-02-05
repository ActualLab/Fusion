using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Abstract base for database processors that have access to an <see cref="IDbHub"/>
/// and common infrastructure.
/// </summary>
public abstract class DbProcessorBase(IDbHub dbHub) : ProcessorBase
{
    protected IServiceProvider Services { get; init; } = dbHub.Services;
    protected IDbHub DbHub { get; } = dbHub;
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}

/// <summary>
/// Abstract base for database processors scoped to a specific <typeparamref name="TDbContext"/>,
/// with lazy access to <see cref="DbHub{TDbContext}"/>.
/// </summary>
public abstract class DbProcessorBase<TDbContext>(IServiceProvider services) : ProcessorBase
    where TDbContext : DbContext
{
    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => field ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}
