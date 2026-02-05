namespace ActualLab.DependencyInjection;

/// <summary>
/// Indicates a type that supports post-construction initialization with optional settings.
/// </summary>
public interface IHasInitialize
{
    public void Initialize(object? settings = null);
}
