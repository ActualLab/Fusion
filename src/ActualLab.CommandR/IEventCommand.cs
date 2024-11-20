namespace ActualLab.CommandR;

public interface IEventCommand : ICommand<Unit>
{
    public Symbol ChainId { get; init; }
}
