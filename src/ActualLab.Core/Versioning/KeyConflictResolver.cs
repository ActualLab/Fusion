using ActualLab.Internal;

namespace ActualLab.Versioning;

/// <summary>
/// A delegate that resolves a key conflict between a new entity and an existing one.
/// </summary>
public delegate TEntity KeyConflictResolver<TEntity>(TEntity entity, TEntity existing);

/// <summary>
/// Factory methods for creating <see cref="KeyConflictResolver{TEntity}"/> delegates
/// from <see cref="KeyConflictStrategy"/> values.
/// </summary>
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

    /// <summary>
    /// Provides a conflict resolver that throws on key conflict.
    /// </summary>
    private static class FailHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (_, _) => throw Error<TEntity>();
    }

    /// <summary>
    /// Provides a conflict resolver that keeps the existing entity on key conflict.
    /// </summary>
    private static class SkipHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (_, existing) => existing;
    }

    /// <summary>
    /// Provides a conflict resolver that replaces the existing entity on key conflict.
    /// </summary>
    private static class ReplaceHandler<TEntity>
    {
        public static readonly KeyConflictResolver<TEntity> Instance
            = (entity, _) => entity;
    }
}
