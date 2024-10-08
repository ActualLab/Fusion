namespace ActualLab.Collections;

public static class SpanLikeExt
{
    // GetValueOrDefault

    public static T? GetValueOrDefault<T>(this T[] items, int index)
        => index < 0 || index >= items.Length ? default : items[index];

    public static T? GetValueOrDefault<T>(this ReadOnlySpan<T> items, int index)
        => index < 0 || index >= items.Length ? default : items[index];

    public static T? GetValueOrDefault<T>(this ImmutableArray<T> items, int index)
        => index < 0 || index >= items.Length ? default : items[index];

    public static T? GetValueOrDefault<T>(this IReadOnlyList<T> items, int index)
        => index < 0 || index >= items.Count ? default : items[index];

    // GetRingItem

    public static T GetRingItem<T>(this T[] items, int index)
        => items[index.PositiveModulo(items.Length)];

    public static T GetRingItem<T>(this ReadOnlySpan<T> items, int index)
        => items[index.PositiveModulo(items.Length)];

    public static T GetRingItem<T>(this ImmutableArray<T> items, int index)
        => items[index.PositiveModulo(items.Length)];

    public static T GetRingItem<T>(this IReadOnlyList<T> items, int index)
        => items[index.PositiveModulo(items.Count)];
}
