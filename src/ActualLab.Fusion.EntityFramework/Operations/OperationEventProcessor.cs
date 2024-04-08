using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class OperationEventProcessor
{
    protected IServiceProvider Services { get; }
    protected ICommander Commander { get; }
    protected ILogger Log { get; }

    public OperationEventProcessor(IServiceProvider services)
    {
        Services = services;
        Commander = services.Commander();
        Log = services.LogFor(GetType());
    }

    public virtual Task Process(OperationEvent @event, CancellationToken cancellationToken)
    {
        var ulid = @event.Uuid;
        var value = @event.Value;
        if (value is ICommand command) {
            Log.LogInformation("Processing command event {Ulid}: {Command}", ulid, command);
            return Commander.Run(command, true, cancellationToken);
        }
        Log.LogInformation("Unsupported event {Ulid}: {Event}", ulid, value);
        return Task.CompletedTask;
    }
}
