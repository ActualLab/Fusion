using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public interface ICommander : IHasServices
{
    public CommanderHub Hub { get; }

    public Task<CommandContext> Run(CommandContext context, CancellationToken cancellationToken = default);
}
