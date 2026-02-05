using ActualLab.Mathematics.Internal;

namespace ActualLab.Mathematics;

/// <summary>
/// Resolves <see cref="Arithmetics{T}"/> instances for a given type.
/// </summary>
public abstract class ArithmeticsProvider
{
    public static ArithmeticsProvider Default { get; set; } = new DefaultArithmeticsProvider();

    public abstract Arithmetics<T> Get<T>()
        where T : notnull;
}
