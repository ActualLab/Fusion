namespace ActualLab.Caching;

public interface IMaybeCachedValue
{
    Task WhenSynchronized { get; }
}
