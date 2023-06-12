namespace Stl.Tests.CommandR.Services;

public class LogEnterExitService : ServiceBase, ICommandService
{
    public LogEnterExitService(IServiceProvider services) : base(services) { }

    [CommandFilter(1000)]
    public async Task OnAnyCommand(
        ICommand command, CommandContext context,
        CancellationToken cancellationToken)
    {
        Log.LogInformation("+ {Command}", command);
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            await LogResult((dynamic) command);
        }
        catch (Exception e) {
            Log.LogError(e, "- {Command} !-> error", command);
            throw;
        }
    }

    protected async Task LogResult<T>(ICommand<T> command)
    {
        var context = (CommandContext<T>) CommandContext.Current!;
        var resultTask = context.ResultTask;
        if (!resultTask.IsCompleted) {
            Log.LogInformation("- {Command} -> {Result}", command, default(T));
            return;
        }
        var result = await resultTask.ConfigureAwait(false);
        Log.LogInformation("- {Command} -> {Result}", command, result);
    }
}
