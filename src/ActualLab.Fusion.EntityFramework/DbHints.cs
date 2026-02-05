namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Provides commonly used <see cref="DbHint"/> arrays for query locking and wait behavior.
/// </summary>
public static class DbHintSet
{
    public static readonly DbHint[] Empty = [];
    public static readonly DbHint[] Update = [DbLockingHint.Update];
    public static readonly DbHint[] UpdateSkipLocked = [DbLockingHint.Update, DbWaitHint.SkipLocked];
}

/// <summary>
/// Base record for database query hints that influence SQL generation (e.g., locking).
/// </summary>
public abstract record DbHint(string Value);

/// <summary>
/// A <see cref="DbHint"/> representing row-level locking modes (e.g., Share, Update).
/// </summary>
public record DbLockingHint(string Value) : DbHint(Value)
{
    public static readonly DbLockingHint KeyShare = new(nameof(KeyShare));
    public static readonly DbLockingHint Share = new(nameof(Share));
    public static readonly DbLockingHint NoKeyUpdate = new(nameof(NoKeyUpdate));
    public static readonly DbLockingHint Update = new(nameof(Update));
}

/// <summary>
/// A <see cref="DbHint"/> representing lock wait behavior (e.g., NoWait, SkipLocked).
/// </summary>
public record DbWaitHint(string Value) : DbHint(Value)
{
    public static readonly DbLockingHint NoWait = new(nameof(NoWait));
    public static readonly DbLockingHint SkipLocked = new(nameof(SkipLocked));
}

/// <summary>
/// A <see cref="DbHint"/> containing a custom SQL hint string passed directly to the formatter.
/// </summary>
public record DbCustomHint(string Value) : DbHint(Value);
