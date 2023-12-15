namespace ActualLab.DependencyInjection;

public interface IHasInitialize
{
    void Initialize(object? settings = null);
}
