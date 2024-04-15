namespace ActualLab.CommandR.Operations;

public sealed record OperationEvent(
    Symbol Uuid,
    Moment LoggedAt,
    Moment DelayUntil,
    object? Value
    ) : IHasUuid, IHasId<Symbol>
{
    Symbol IHasId<Symbol>.Id => Uuid;
}
