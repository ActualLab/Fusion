namespace ActualLab.CommandR;

public interface IHasCommandId
{
    Ulid CommandId { get; init; }
}
