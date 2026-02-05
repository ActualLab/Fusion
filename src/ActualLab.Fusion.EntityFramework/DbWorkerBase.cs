using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Abstract base for long-running database workers that have access to an
/// <see cref="IDbHub"/> and common services.
/// </summary>
public abstract class DbWorkerBase(
    IDbHub dbHub,
    CancellationTokenSource? stopTokenSource = null
    ) : WorkerBase(stopTokenSource)
{
    protected IServiceProvider Services { get; init; } = dbHub.Services;
    protected IDbHub DbHub { get; } = dbHub;
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}

/// <summary>
/// Abstract base for long-running database workers scoped to a specific
/// <typeparamref name="TDbContext"/>, with access to <see cref="DbHub{TDbContext}"/>.
/// </summary>
public abstract class DbWorkerBase<TDbContext>(
    IServiceProvider services,
    CancellationTokenSource? stopTokenSource = null
    ) : WorkerBase(stopTokenSource)
    where TDbContext : DbContext
{
    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => field ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => field ??= Services.LogFor(GetType());
}
