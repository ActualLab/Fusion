using System.Diagnostics;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Diagnostics;

/// <summary>
/// A command filter that creates OpenTelemetry activities for command execution
/// and logs errors.
/// </summary>
public class CommandTracer(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;

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
        catch (Exception e) when (activity is not null) {
            if (!ActivityExt.IsError(e))
                throw; // Don't log non-error

            activity.Finalize(e, cancellationToken);
            var message = context.IsOutermost ?
                "Outermost command failed: {Command}" :
                "Nested command failed: {Command}";
            var level = activity.Status is ActivityStatusCode.Error ? LogLevel.Error : LogLevel.Warning;
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.IfEnabled(level)?.Log(level, e, message, command);
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
        for (var c = context; c is not null; c = c.OuterContext)
            if (c.UntypedCommand is ISystemCommand)
                return false;

        // Trace the rest
        return true;
    }

    protected virtual Activity? StartActivity(ICommand command, CommandContext context)
    {
        var operationName = command.GetOperationName();
        var activity = CommanderInstruments.ActivitySource.StartActivity(operationName);
        if (activity is not null) {
            var tags = new ActivityTagsCollection { { "command", command.ToString() } };
            var activityEvent = new ActivityEvent(operationName, tags: tags);
            activity.AddEvent(activityEvent);
        }
        return activity;
    }
}
