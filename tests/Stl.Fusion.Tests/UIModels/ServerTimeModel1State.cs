using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests.UIModels;

public class ServerTimeModel1State(IServiceProvider services)
    : ComputedState<ServerTimeModel1>(new() { InitialValue = new(default) }, services)
{
    private ITimeService TimeService => Services.GetRequiredService<ITimeService>();

    protected override async Task<ServerTimeModel1> Compute(CancellationToken cancellationToken)
    {
        var time = await TimeService.GetTime(cancellationToken).ConfigureAwait(false);
        return new ServerTimeModel1(time);
    }
}
