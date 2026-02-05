namespace ActualLab.CommandR;

/// <summary>
/// Represents an event command that can be dispatched to multiple handler chains
/// identified by <see cref="ChainId"/>.
/// </summary>
public interface IEventCommand : ICommand<Unit>
{
    public string ChainId { get; init; }
}
