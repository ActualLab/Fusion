namespace ActualLab.CommandR.Operations;

public readonly record struct OperationEvent(
    Symbol Uuid,
    Moment FiresAt,
    object? Value
    ) : IHasUuid, IHasId<Symbol>
{
    Symbol IHasId<Symbol>.Id => Uuid;

    // Computed
    public bool HasFiresAt => FiresAt != default;
}
