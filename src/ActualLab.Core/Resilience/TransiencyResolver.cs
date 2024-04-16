namespace ActualLab.Resilience;

/// <summary>
/// Tells if an error is transient (might be gone on retry).
/// </summary>
public delegate Transiency TransiencyResolver(Exception error);

/// <summary>
/// Tells if an error is transient (might be gone on retry).
/// </summary>
/// <typeparam name="TContext">A context this detector is applicable to -
/// typically DbContext or something similar.</typeparam>
public delegate Transiency TransiencyResolver<TContext>(Exception error)
    where TContext : class;

public static class TransiencyResolverExt
{
    public static TransiencyResolver<TContext> ForContext<TContext>(this TransiencyResolver resolver)
        where TContext : class
        => resolver.Invoke;
}
