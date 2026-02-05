using ActualLab.CommandR.Operations;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// An <see cref="IOperationCompletionListener"/> that produces <see cref="ICompletion"/> commands
/// for completed operations to trigger their invalidation pass.
/// </summary>
public class CompletionProducer(CompletionProducer.Options settings, IServiceProvider services)
    : IOperationCompletionListener
{
    /// <summary>
    /// Configuration options for <see cref="CompletionProducer"/>.
    /// </summary>
    public record Options
    {
        public bool IgnoreNotLogged { get; init; } = false;
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
    }

    protected Options Settings { get; } = settings;
    protected ICommander Commander { get; } = services.Commander();
    protected RpcHub RpcHub { get; } = services.RpcHub();
    protected IServiceProvider Services => Commander.Services;
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public virtual Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        if (operation.Command is not { } command)
            return Task.CompletedTask; // We skip operations w/o Command

        return Task.Run(async () => {
            var isLocal = commandContext is not null;
            var operationType = isLocal ? "Local" : "External";
            try {
                var completion = Completion.New(operation);
                var context = CommandContext.New(Commander, completion, isOutermost: true);
                context.Items.KeylessSet(new RpcOutboundCallSetup(RpcHub.LocalPeer)); // Override possible routing to a remote peer
                await context.Call(CancellationToken.None).ConfigureAwait(false);
                if (command is not INotLogged || Settings.IgnoreNotLogged)
                    Log.IfEnabled(Settings.LogLevel)?.Log(Settings.LogLevel,
                        "{OperationType} operation completion succeeded. Host: {HostId}, Command: {Command}",
                        operationType, operation.HostId, command);
            }
            catch (Exception e) {
                Log.LogError(e,
                    "{OperationType} operation completion failed! Host: {HostId}, Command: {Command}",
                    operationType, operation.HostId, command);
            }
        });
    }
}
