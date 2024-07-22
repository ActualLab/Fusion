namespace ActualLab.Fusion.EntityFramework;

public static class DbHintSet
{
    public static readonly DbHint[] Empty = [];
    public static readonly DbHint[] Update = [DbLockingHint.Update];
    public static readonly DbHint[] UpdateSkipLocked = [DbLockingHint.Update, DbWaitHint.SkipLocked];
}

public abstract record DbHint(Symbol Value);

public record DbLockingHint(Symbol Value) : DbHint(Value)
{
    public static readonly DbLockingHint KeyShare = new(nameof(KeyShare));
    public static readonly DbLockingHint Share = new(nameof(Share));
    public static readonly DbLockingHint NoKeyUpdate = new(nameof(NoKeyUpdate));
    public static readonly DbLockingHint Update = new(nameof(Update));
}

public record DbWaitHint(Symbol Value) : DbHint(Value)
{
    public static readonly DbLockingHint NoWait = new(nameof(NoWait));
    public static readonly DbLockingHint SkipLocked = new(nameof(SkipLocked));
}

public record DbCustomHint(Symbol Value) : DbHint(Value);
