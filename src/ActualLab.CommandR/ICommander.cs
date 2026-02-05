using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

/// <summary>
/// The main entry point for executing commands through the handler pipeline.
/// </summary>
public interface ICommander : IHasServices
{
    public CommanderHub Hub { get; }

    public Task<CommandContext> Run(CommandContext context, CancellationToken cancellationToken = default);
}
