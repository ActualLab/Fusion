using System.Data;

namespace ActualLab.Fusion.EntityFramework;

public static class FusionBuilderExt
{
    // AddGlobalIsolationLevelSelector

    public static FusionBuilder AddGlobalIsolationLevelSelector(
        this FusionBuilder fusion,
        Func<IServiceProvider, DbIsolationLevelSelector> globalIsolationLevelSelector)
    {
        fusion.Services.AddSingleton(globalIsolationLevelSelector);
        return fusion;
    }

    public static FusionBuilder AddGlobalIsolationLevelSelector(
        this FusionBuilder fusion,
        Func<IServiceProvider, CommandContext, IsolationLevel> globalIsolationLevelSelector)
    {
        fusion.Services.AddSingleton(c => new DbIsolationLevelSelector(
            context => globalIsolationLevelSelector.Invoke(c, context)));
        return fusion;
    }
}
