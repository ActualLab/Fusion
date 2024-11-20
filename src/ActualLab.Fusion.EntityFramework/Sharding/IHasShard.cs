namespace ActualLab.Fusion.EntityFramework;

public interface IHasShard
{
    public DbShard Shard { get; }
}
