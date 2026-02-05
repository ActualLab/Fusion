namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Indicates that the implementing type has access to a <see cref="CircuitHub"/> instance.
/// </summary>
public interface IHasCircuitHub : IHasServices
{
    public CircuitHub CircuitHub { get; }
}
