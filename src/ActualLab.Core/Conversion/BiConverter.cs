namespace ActualLab.Conversion;

public static class BiConverter
{
    public static BiConverter<T, T> Identity<T>() => Cache<T>.Identity;

    public static BiConverter<TFrom, TTo> New<TFrom, TTo>(Func<TFrom, TTo> forward, Func<TTo, TFrom> backward)
        => new(forward, backward);

    // Nested types

    private static class Cache<T>
    {
        public static readonly BiConverter<T, T> Identity = new(x => x, x => x);
    }
}

public record struct BiConverter<TFrom, TTo>(Func<TFrom, TTo> Forward, Func<TTo, TFrom> Backward)
{
    public BiConverter<TTo, TFrom> Invert()
        => new(Backward, Forward);
}
