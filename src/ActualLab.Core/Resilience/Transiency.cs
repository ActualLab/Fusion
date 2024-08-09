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
    public static Transiency Or(this Transiency first, Transiency second)
        => first != Transiency.Unknown ? first : second;
    public static Transiency Or(this Transiency first, Exception error, TransiencyResolver second)
        => first != Transiency.Unknown ? first : second.Invoke(error);

    public static bool MustRetry(this Transiency transiency, bool retryOnNonTransient = false)
        => !transiency.IsTerminal() && (retryOnNonTransient || transiency.IsTransient());

    public static bool IsTerminal(this Transiency transiency)
        => transiency is Transiency.Terminal;

    public static bool IsNonTransient(this Transiency transiency)
        => !(transiency is Transiency.Transient or Transiency.SuperTransient);

    public static bool IsTransient(this Transiency transiency)
        => transiency is Transiency.Transient or Transiency.SuperTransient;

    public static bool IsSuperTransient(this Transiency transiency)
        => transiency == Transiency.SuperTransient;
}
