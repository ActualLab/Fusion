namespace ActualLab.CommandR;

public interface IEventCommand : ICommand<Unit>
{
    Symbol ChainId { get; init; }
}
