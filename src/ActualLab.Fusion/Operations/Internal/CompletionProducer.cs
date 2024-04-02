using ActualLab.CommandR.Operations;
using ActualLab.OS;

namespace ActualLab.Fusion.Operations.Internal;

public class CompletionProducer(CompletionProducer.Options settings, ICommander commander)
    : IOperationCompletionListener
{
    public record Options
    {
        public bool IgnoreNotLogged { get; init; } = false;
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
    }

    private ILogger? _log;

    protected Options Settings { get; } = settings;
    protected ICommander Commander { get; } = commander;
    protected IServiceProvider Services => Commander.Services;
    protected HostId HostId => Commander.HostId;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public bool IsReady()
        => true;

    public virtual Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        if (operation.Command is not { } command)
            return Task.CompletedTask; // We skip operations w/o Command

        return Task.Run(async () => {
            var isLocal = commandContext != null;
            var operationType = isLocal ? "Local" : "External";
            try {
                await Commander.Call(Completion.New(operation), true).ConfigureAwait(false);
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
