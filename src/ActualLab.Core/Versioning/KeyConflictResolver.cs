using ActualLab.Internal;

namespace ActualLab.Versioning;

public delegate TEntity KeyConflictResolver<TEntity>(TEntity entity, TEntity existing);

public static class KeyConflictResolver
{
    public static KeyConflictResolver<TEntity> For<TEntity>(KeyConflictStrategy strategy)
        => strategy switch {
            KeyConflictStrategy.Fail => FailHandler<TEntity>.Instance,
            KeyConflictStrategy.Skip => SkipHandler<TEntity>.Instance,
            KeyConflictStrategy.Update => ReplaceHandler<TEntity>.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };

    public static Exception Error<TEntity>()
        => Errors.KeyAlreadyExists<TEntity>();

    // Private methods

    private static class FailHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (_, _) => throw Error<TEntity>();
    }

    private static class SkipHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (_, existing) => existing;
    }

    private static class ReplaceHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (entity, _) => entity;
    }
}
