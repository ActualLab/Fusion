namespace ActualLab.CommandR;

public interface ICommander : IHasServices
{
    HostId HostId { get; }
    MomentClockSet Clocks { get; }

    Task Run(CommandContext context, CancellationToken cancellationToken = default);
}
