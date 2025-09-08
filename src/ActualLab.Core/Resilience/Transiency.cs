namespace ActualLab.Resilience;

public enum Transiency
{
    Unknown = 0, // Treated as NonTransient
    Transient,
    SuperTransient, // A transient error which requires infinite retries
    NonTransient,
    Terminal,
}

public static class TransiencyExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Transiency Or(this Transiency first, Transiency second)
        => first != Transiency.Unknown ? first : second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Transiency Or(this Transiency first, Exception error, TransiencyResolver second)
        => first != Transiency.Unknown ? first : second.Invoke(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAnyTransient(this Transiency transiency)
        => transiency is Transiency.Transient or Transiency.SuperTransient;
}
