namespace ActualLab.Conversion;

/// <summary>
/// Factory methods for creating <see cref="BiConverter{TFrom, TTo}"/> instances.
/// </summary>
public static class BiConverter
{
    public static BiConverter<T, T> Identity<T>() => Cache<T>.Identity;

    public static BiConverter<TFrom, TTo> New<TFrom, TTo>(Func<TFrom, TTo> forward, Func<TTo, TFrom> backward)
        => new(forward, backward);

    // Nested types

    /// <summary>
    /// Caches the identity <see cref="BiConverter{TFrom, TTo}"/> for type <typeparamref name="T"/>.
    /// </summary>
    private static class Cache<T>
    {
        public static readonly BiConverter<T, T> Identity = new(x => x, x => x);
    }
}

/// <summary>
/// A bidirectional converter that provides both forward and backward conversion functions.
/// </summary>
public record struct BiConverter<TFrom, TTo>(Func<TFrom, TTo> Forward, Func<TTo, TFrom> Backward)
{
    public BiConverter<TTo, TFrom> Invert()
        => new(Backward, Forward);
}
