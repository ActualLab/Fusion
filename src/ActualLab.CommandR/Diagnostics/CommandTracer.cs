using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Diagnostics;

public class CommandTracer(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;

    [field: AllowNull, MaybeNull]
    protected ILogger Log {
        get => field ??= Services.LogFor(GetType());
        init;
    }

    public LogLevel ErrorLogLevel { get; init; } = LogLevel.Error;

    [CommandFilter(Priority = CommanderCommandHandlerPriority.CommandTracer)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        if (!ShouldTrace(command, context)) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var activity = StartActivity(command, context);
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            if (activity == null || !ActivityExt.IsError(e))
                throw;

            activity.Finalize(e, cancellationToken);
            var message = context.IsOutermost ?
                "Outermost command failed: {Command}" :
                "Nested command failed: {Command}";
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.IfEnabled(LogLevel.Error)?.Log(ErrorLogLevel, e, message, command);
            throw;
        }
    }

    // Protected methods

    protected virtual bool ShouldTrace(ICommand command, CommandContext context)
    {
        // Always trace top-level commands
        if (context.IsOutermost)
            return true;

        // Do not trace system commands & any nested command they run
        for (var c = context; c != null; c = c.OuterContext)
            if (c.UntypedCommand is ISystemCommand)
                return false;

        // Trace the rest
        return true;
    }

    protected virtual Activity? StartActivity(ICommand command, CommandContext context)
    {
        var operationName = command.GetOperationName();
        var activity = CommanderInstruments.ActivitySource.StartActivity(operationName);
        if (activity != null) {
            var tags = new ActivityTagsCollection { { "command", command.ToString() } };
            var activityEvent = new ActivityEvent(operationName, tags: tags);
            activity.AddEvent(activityEvent);
        }
        return activity;
    }
}
