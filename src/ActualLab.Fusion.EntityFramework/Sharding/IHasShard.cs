namespace ActualLab.Fusion.EntityFramework;

public interface IHasShard
{
    DbShard Shard { get; }
}
