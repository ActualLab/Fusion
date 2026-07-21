using System.Diagnostics;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Diagnostics;

/// <summary>
/// A command filter that creates OpenTelemetry activities for command execution
/// and logs errors.
/// </summary>
public class CommandTracer(CommandTracer.Options settings, IServiceProvider services) : ICommandHandler<ICommand>
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public bool CaptureCommandPayload { get; init; }
    }

    protected IServiceProvider Services { get; } = services;
    protected Options Settings { get; } = settings;

    protected ILogger Log {
        get => field ??= Services.LogFor(GetType());
        init;
    }

    public LogLevel ErrorLogLevel { get; init; } = LogLevel.Error;

    [CommandFilter(Priority = CommanderCommandHandlerPriority.CommandTracer)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var mustTrace = ShouldTrace(command, context);
        var durationHistogram = CommanderInstruments.CommandExecutionDuration.IfEnabled();
        if (!mustTrace && durationHistogram is null) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var startedAt = durationHistogram is not null ? CpuTimestamp.Now : default;
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
            if (durationHistogram is not null) {
                var tags = new TagList {
                    { "command.name", command.GetType().GetName() },
                    { "command.kind", command is IEventCommand ? "event" : "command" },
                    { "command.scope", context.IsOutermost ? "outermost" : "nested" },
                    { "outcome", outcome },
                };
                durationHistogram.Record(startedAt.Elapsed.TotalMilliseconds, tags);
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
        var commandName = command.GetType().GetName();
        var operationName = $"cmd.{DiagnosticsExt.FixName(commandName)}";
        var activity = CommanderInstruments.ActivitySource.StartActivity(operationName);
        activity?.AddCommandTags(
            command,
            Settings.CaptureCommandPayload,
            context.IsOutermost ? "outermost" : "nested");
        return activity;
    }
}
