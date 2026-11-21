using System.Buffers;

namespace ActualLab.Collections.Internal;

internal static class ArrayPoolExt
{
    public static void Resize<T>(this ArrayPool<T> pool, ref T[] array, int newSize, bool mustClear)
    {
        if (array.Length == newSize)
            return;

        var newArray = pool.Rent(newSize);
        Array.Copy(array, newArray, Math.Min(array.Length, newSize));
        pool.Return(array, mustClear);
        array = newArray;
    }
}
