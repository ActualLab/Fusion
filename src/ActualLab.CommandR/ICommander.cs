using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public interface ICommander : IHasServices
{
    CommanderHub Hub { get; }

    Task Run(CommandContext context, CancellationToken cancellationToken = default);
}
