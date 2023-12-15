namespace ActualLab.CommandR;

public interface ICommander : IHasServices
{
    Task Run(CommandContext context, CancellationToken cancellationToken = default);
}
