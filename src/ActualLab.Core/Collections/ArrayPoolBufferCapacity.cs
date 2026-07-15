namespace ActualLab.Collections;

internal static class ArrayPoolBufferCapacity
{
    private const int MinCapacity = 16;
    private const int MaxCapacity = 1 << 30;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRequired(int position, int sizeHint)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        var increment = Math.Max(1, sizeHint);
        if (position > MaxCapacity - increment)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        return position + increment;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Round(int capacity)
    {
        if (capacity < 0 || capacity > MaxCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        return Math.Max(MinCapacity, (int)Bits.GreaterOrEqualPowerOf2((ulong)capacity));
    }
}
