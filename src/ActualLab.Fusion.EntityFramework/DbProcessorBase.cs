using System.Data;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public abstract class DbProcessorBase<TDbContext>(IServiceProvider services) : ProcessorBase
    where TDbContext : DbContext
{
    private ILogger? _log;
    private DbHub<TDbContext>? _dbHub;

    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => _dbHub ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected IsolationLevel CommandIsolationLevel {
        get => DbHub.CommandIsolationLevel;
        set => DbHub.CommandIsolationLevel = value;
    }
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => _log ??= Services.LogFor(GetType());
}
