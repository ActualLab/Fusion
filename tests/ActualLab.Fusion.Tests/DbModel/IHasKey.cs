namespace ActualLab.Fusion.Tests.DbModel;

public interface IHasKey<out TKey>
    where TKey : notnull
{
    public TKey Key { get; }
}
