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
        var mustTrace = ShouldTrace(command, context);
        var mustMeasure = CommanderInstruments.CommandExecutionDuration.Enabled;
        if (!mustTrace && !mustMeasure) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var startedAt = mustMeasure ? CpuTimestamp.Now : default;
        using var activity = mustTrace ? StartActivity(command, context) : null;
        var outcome = "success";
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            outcome = e is OperationCanceledException ? "cancel" : "error";
            if (activity is not null && ActivityExt.IsError(e)) {
                activity.Finalize(e, cancellationToken);
                var message = context.IsOutermost ?
                    "Outermost command failed: {Command}" :
                    "Nested command failed: {Command}";
                var level = activity.Status is ActivityStatusCode.Error ? LogLevel.Error : LogLevel.Warning;
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Log.IfEnabled(level)?.Log(level, e, message, command);
            }
            throw;
        }
        finally {
            if (mustMeasure) {
                var tags = new TagList {
                    { "command.name", command.GetType().NonProxyType().GetName() },
                    { "command.kind", command is IEventCommand ? "event" : "command" },
                    { "command.scope", context.IsOutermost ? "outermost" : "nested" },
                    { "outcome", outcome },
                };
                CommanderInstruments.CommandExecutionDuration.Record(startedAt.Elapsed.TotalMilliseconds, tags);
            }
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
