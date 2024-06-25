namespace ActualLab.Fusion.EntityFramework.Internal;

public static class Errors
{
    public static Exception DbContextIsReadOnly()
        => new InvalidOperationException("This DbContext is read-only.");

    public static Exception WrongDbOperationScopeShard(Type scopeType, DbShard shard, DbShard requestedShard)
        => new InvalidOperationException($"{scopeType} is already bound to shard '{shard}', which differs from '{requestedShard}'.");
    public static Exception DbOperationIndexWasNotAssigned()
        => new InvalidOperationException("DbOperation.Index wasn't assigned on save.");

    public static Exception NoShard(DbShard shard)
        => new InvalidOperationException($"Shard doesn't exist: '{shard}'.");

    public static Exception EntityNotFound<TEntity>()
        => EntityNotFound(typeof(TEntity));
    public static Exception EntityNotFound(Type entityType)
        => new KeyNotFoundException($"Requested {entityType.GetName()} entity is not found.");

    public static Exception InvalidUserId()
        => new FormatException("Invalid UserId.");
    public static Exception UserIdRequired()
        => new FormatException("UserId is None, even though a valid one is expected here.");

    public static Exception UnsupportedDbHint(DbHint hint)
        => new NotSupportedException($"Unsupported DbHint: {hint}");

    public static Exception BatchSizeCannotBeChanged()
        => new InvalidOperationException("ConfigureBatchProcessor delegate cannot change BatchProcessor's BatchSize.");
    public static Exception BatchSizeIsTooLarge()
        => new InvalidOperationException("DbEntityResolver's BatchSize is too large.");
    public static Exception CannotCompileQuery()
        => new InvalidOperationException("DbEntityResolver is unable to produce compiled query.");
}
