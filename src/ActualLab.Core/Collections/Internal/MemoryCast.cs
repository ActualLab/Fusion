namespace ActualLab.Collections.Internal;

/// <summary>
/// Reinterprets <see cref="ReadOnlyMemory{T}"/> of one unmanaged type as another without copying.
/// </summary>
internal static class MemoryCast
{
    public static ReadOnlyMemory<TTo> Cast<TFrom, TTo>(ReadOnlyMemory<TFrom> source)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        if (source.IsEmpty)
            return default;
        if (typeof(TFrom) == typeof(TTo))
            return Unsafe.As<ReadOnlyMemory<TFrom>, ReadOnlyMemory<TTo>>(ref source);

        // Casting back to the original type returns the original memory when the byte offsets align
        if (MemoryMarshal.TryGetMemoryManager<TFrom, CastMemoryManager<TTo, TFrom>>(
            source, out var manager, out var start, out var length)) {
            var startBytes = start * Unsafe.SizeOf<TFrom>();
            var lengthBytes = length * Unsafe.SizeOf<TFrom>();
            var toSize = Unsafe.SizeOf<TTo>();
            if (startBytes % toSize == 0 && lengthBytes % toSize == 0)
                return manager.Source.Slice(startBytes / toSize, lengthBytes / toSize);
        }

        return new CastMemoryManager<TFrom, TTo>(source).Memory;
    }
}
