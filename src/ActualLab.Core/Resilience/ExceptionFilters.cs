namespace ActualLab.Resilience;

/// <summary>
/// Provides common <see cref="ExceptionFilter"/> instances for transient,
/// non-transient, terminal, and unconditional matching.
/// </summary>
public static class ExceptionFilters
{
    public static readonly ExceptionFilter Any = (_, _) => true;
    public static readonly ExceptionFilter None = (_, _) => false;
    public static readonly ExceptionFilter AnyTerminal = (_, transiency) => transiency is Transiency.Terminal;
    public static readonly ExceptionFilter AnyNonTerminal = (_, transiency) => transiency is not Transiency.Terminal;
    public static readonly ExceptionFilter AnyTransient = (_, transiency) => transiency.IsAnyTransient();
    public static readonly ExceptionFilter AnyNonTransient = (_, transiency) => !transiency.IsAnyTransient();
}
