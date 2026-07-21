using System.Diagnostics;

namespace ActualLab.CommandR.Diagnostics;

public static class CommandActivityExt
{
    public static void AddCommandTags(
        this Activity activity,
        ICommand command,
        bool capturePayload,
        string? scope = null)
    {
        var commandName = command.GetType().GetName();
        activity.SetTag("command.name", commandName);
        activity.SetTag("command.kind", command is IEventCommand ? "event" : "command");
        if (scope is not null)
            activity.SetTag("command.scope", scope);
        if (!capturePayload || !activity.IsAllDataRequested)
            return;

        try {
            var payload = command.ToString();
            if (payload is not null)
                activity.AddEvent(new ActivityEvent("command", tags: new ActivityTagsCollection {
                    { "command.payload", payload },
                }));
        }
        catch (Exception e) {
            activity.AddEvent(new ActivityEvent("command.payload.error", tags: new ActivityTagsCollection {
                { "exception.type", e.GetType().FullName ?? e.GetType().Name },
            }));
        }
    }
}
