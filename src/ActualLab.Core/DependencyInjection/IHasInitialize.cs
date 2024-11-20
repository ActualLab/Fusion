namespace ActualLab.DependencyInjection;

public interface IHasInitialize
{
    public void Initialize(object? settings = null);
}
