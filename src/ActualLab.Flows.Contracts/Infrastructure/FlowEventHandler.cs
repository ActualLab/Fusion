using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;

namespace ActualLab.Flows.Infrastructure;

public sealed class FlowEventHandler : ICommandHandler<IFlowEvent>
{
    private FlowSerializer Serializer { get; }
    private ICommander Commander { get; }
    private ILogger Log { get; }

    public FlowEventHandler(IServiceProvider services)
    {
        Log = services.LogFor(GetType());
        Serializer = services.GetRequiredService<FlowSerializer>();
        Commander = services.Commander();
    }

    [CommandHandler(IsFilter = false)]
    public async Task OnCommand(IFlowEvent command, CommandContext context, CancellationToken cancellationToken)
    {
        var uuid = command is IHasUuid hasUuid
            ? hasUuid.Uuid
            : (Symbol)Commander.Hub.UuidGenerator.Next();
        var eventDataCommand = new Flows_EventData(uuid, command.FlowId, Serializer.SerializeEvent(command));
        var version = await Commander.Call(eventDataCommand, true, cancellationToken).ConfigureAwait(false);
        context.SetResult(version);
    }
}
