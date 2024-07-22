using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public abstract class DbProcessorBase(IDbHub dbHub) : ProcessorBase
{
    private ILogger? _log;

    protected IServiceProvider Services { get; init; } = dbHub.Services;
    protected IDbHub DbHub { get; } = dbHub;
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => _log ??= Services.LogFor(GetType());
}

public abstract class DbProcessorBase<TDbContext>(IServiceProvider services) : ProcessorBase
    where TDbContext : DbContext
{
    private ILogger? _log;
    private DbHub<TDbContext>? _dbHub;

    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => _dbHub ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => _log ??= Services.LogFor(GetType());
}
