namespace ActualLab;

/// <summary>
/// Similar to <see cref="IHasId{TId}"/>, but indicates the Id is universally unique.
/// </summary>
public interface IHasUuid
{
    public Symbol Uuid { get; }
}
