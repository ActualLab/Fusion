using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Flows.Services;

public record DbFlowsDependencies(IDbHub DbHub) : IHasServices
{
    public IServiceProvider Services => DbHub.Services;
}
