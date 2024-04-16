using ActualLab.CommandR.Operations;
using ActualLab.Resilience;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbEventProcessor<TDbContext>(IServiceProvider services)
    : DbServiceBase<TDbContext>(services)
    where TDbContext : DbContext
{
    public virtual async Task Process(OperationEvent operationEvent, CancellationToken cancellationToken)
    {
        if (DbHub.ChaosMaker.IsEnabled)
            await DbHub.ChaosMaker.Act(operationEvent, cancellationToken).ConfigureAwait(false);

        var ulid = operationEvent.Uuid;
        var value = operationEvent.Value;
        var eventType = value?.GetType().GetName() ?? "null";
        var delay = (operationEvent.DelayUntil - operationEvent.LoggedAt).Positive();
        var processingDelay = Clocks.SystemClock.Now - operationEvent.DelayUntil;
        var info = delay > TimeSpan.FromSeconds(0.1)
            ? $"{ulid} ({delay.ToShortString()} + {processingDelay.ToShortString()} delay)"
            : $"{ulid} ({processingDelay.ToShortString()} delay)";

        if (value is ICommand command) {
            Log.LogInformation("Processing command event {CommandType}: {Info}", eventType, info);
            try {
                await Commander.Call(command, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                throw new TerminalException("Already reprocessed.", e);
            }
            return;
        }
        Log.LogError("Unsupported event {EventType}: {Info}", eventType, info);
    }
}
