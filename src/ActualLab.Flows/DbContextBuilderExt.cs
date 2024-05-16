using ActualLab.Flows.Services;
using ActualLab.Fusion;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Rpc;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Flows;

public static class DbContextBuilderExt
{
    // AddFlows

    public static DbContextBuilder<TDbContext> AddFlows<TDbContext>(
        this DbContextBuilder<TDbContext> dbContext,
        RpcServiceMode serviceMode = RpcServiceMode.Default)
        where TDbContext : DbContext
    {
        var services = dbContext.Services;
        services.AddSingleton(c => new DbFlowsDependencies(c.GetRequiredService<DbHub<TDbContext>>()));
        services.AddFusion().AddService<IFlows, DbFlows>(serviceMode);
        dbContext.AddEntityResolver<string, DbFlow>();
        return dbContext;
    }
}
