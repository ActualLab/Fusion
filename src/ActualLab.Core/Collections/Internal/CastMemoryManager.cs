using System.Buffers;

namespace ActualLab.Collections.Internal;

/// <summary>
/// A <see cref="MemoryManager{T}"/> exposing a <see cref="ReadOnlyMemory{T}"/> of
/// <typeparamref name="TFrom"/> items as <typeparamref name="TTo"/> items without copying.
/// </summary>
internal sealed class CastMemoryManager<TFrom, TTo>(ReadOnlyMemory<TFrom> source) : MemoryManager<TTo>
    where TFrom : unmanaged
    where TTo : unmanaged
{
    public ReadOnlyMemory<TFrom> Source { get; } = source;

    public override Span<TTo> GetSpan()
        => MemoryMarshal.Cast<TFrom, TTo>(MemoryMarshal.AsMemory(Source).Span);

    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if ((uint)elementIndex > (uint)GetSpan().Length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        // Every pin owns its own source pin, which the returned handle releases on Dispose.
        // The holder is allocated first, so a failed allocation can't leak the source pin.
        var sourcePin = new SourcePin();
        sourcePin.Handle = Source.Pin();
        var pointer = (byte*)sourcePin.Handle.Pointer + ((long)elementIndex * sizeof(TTo));
        return new MemoryHandle(pointer, pinnable: sourcePin);
    }

    public override void Unpin()
    { }

    protected override void Dispose(bool disposing)
    { }

    // Nested types

    private sealed class SourcePin : IPinnable
    {
        public MemoryHandle Handle;

        public MemoryHandle Pin(int elementIndex)
            => throw new NotSupportedException();

        public void Unpin()
            => Handle.Dispose();
    }
}
