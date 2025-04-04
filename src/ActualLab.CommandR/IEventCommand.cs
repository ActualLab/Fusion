namespace ActualLab.CommandR;

public interface IEventCommand : ICommand<Unit>
{
    public string ChainId { get; init; }
}
