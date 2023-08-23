using System.Data;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework.Internal;
using Stl.Multitenancy;
using Stl.Versioning;

namespace Stl.Fusion.EntityFramework;

public class DbHub<TDbContext>(IServiceProvider services)
    where TDbContext : DbContext
{
    private ITenantRegistry<TDbContext>? _tenantRegistry;
    private IMultitenantDbContextFactory<TDbContext>? _dbContextFactory;
    private VersionGenerator<long>? _versionGenerator;
    private MomentClockSet? _clocks;
    private ICommander? _commander;
    private ILogger? _log;

    protected ILogger Log => _log ??= Services.LogFor(GetType());
    protected IServiceProvider Services { get; } = services;

    public ITenantRegistry<TDbContext> TenantRegistry
        => _tenantRegistry ??= Services.GetRequiredService<ITenantRegistry<TDbContext>>();
    public IMultitenantDbContextFactory<TDbContext> DbContextFactory
        => _dbContextFactory ??= Services.GetRequiredService<IMultitenantDbContextFactory<TDbContext>>();
    public VersionGenerator<long> VersionGenerator
        => _versionGenerator ??= Services.VersionGenerator<long>();

    public IsolationLevel CommandIsolationLevel {
        get {
            var commandContext = CommandContext.GetCurrent();
            var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
            return operationScope.IsolationLevel;
        }
        set {
            var commandContext = CommandContext.GetCurrent();
            var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
            operationScope.IsolationLevel = value;
        }
    }

    public MomentClockSet Clocks
        => _clocks ??= Services.Clocks();
    public ICommander Commander
        => _commander ??= Services.Commander();

    public TDbContext CreateDbContext(bool readWrite = false)
        => DbContextFactory.CreateDbContext(Tenant.Default).SuppressExecutionStrategy().ReadWrite(readWrite);
    public TDbContext CreateDbContext(Symbol tenantId, bool readWrite = false)
        => DbContextFactory.CreateDbContext(TenantRegistry.Get(tenantId)).SuppressExecutionStrategy().ReadWrite(readWrite);
    public TDbContext CreateDbContext(Tenant tenant, bool readWrite = false)
        => DbContextFactory.CreateDbContext(tenant).SuppressExecutionStrategy().ReadWrite(readWrite);

    public Task<TDbContext> CreateCommandDbContext(CancellationToken cancellationToken = default)
        => CreateCommandDbContext(Tenant.Default, cancellationToken);
    public Task<TDbContext> CreateCommandDbContext(Symbol tenantId, CancellationToken cancellationToken = default)
        => CreateCommandDbContext(TenantRegistry.Get(tenantId), cancellationToken);
    public Task<TDbContext> CreateCommandDbContext(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (Computed.IsInvalidating())
            throw Errors.CreateCommandDbContextIsCalledFromInvalidationCode();

        var commandContext = CommandContext.GetCurrent();
        var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
        var dbContext = CreateDbContext(tenant, readWrite: true);
        return operationScope.InitializeDbContext(dbContext, tenant, cancellationToken);
    }
}
