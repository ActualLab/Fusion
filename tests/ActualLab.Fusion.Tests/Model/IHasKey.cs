namespace ActualLab.Fusion.Tests.Model;

public interface IHasKey<out TKey>
    where TKey : notnull
{
    public TKey Key { get; }
}
