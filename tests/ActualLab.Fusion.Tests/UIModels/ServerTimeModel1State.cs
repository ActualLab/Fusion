using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests.UIModels;

public class ServerTimeModel1State(IServiceProvider services)
    : ComputedState<ServerTimeModel1>(new() { InitialValue = new(default) }, services)
{
    private ITimeService TimeService => Services.GetRequiredService<ITimeService>();

    protected override Task Compute(CancellationToken cancellationToken)
    {
        return Implementation(cancellationToken);

        async Task<ServerTimeModel1> Implementation(CancellationToken cancellationToken)
        {
            if (IsDisposed) // Never complete if the state is already disposed
                await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);

            var time = await TimeService.GetTime(cancellationToken).ConfigureAwait(false);
            return new ServerTimeModel1(time);
        }
    }
}
