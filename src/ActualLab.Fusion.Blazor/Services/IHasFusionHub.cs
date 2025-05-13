namespace ActualLab.Fusion.Blazor;

public interface IHasFusionHub : IHasServices
{
    public FusionHub FusionHub { get; }
}
