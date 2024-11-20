namespace ActualLab.Caching;

public interface IMaybeCachedValue
{
    public Task WhenSynchronized { get; }
}
