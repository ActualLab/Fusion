namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Defines the contract for objects that are associated with a specific database shard.
/// </summary>
public interface IHasShard
{
    public string Shard { get; }
}
