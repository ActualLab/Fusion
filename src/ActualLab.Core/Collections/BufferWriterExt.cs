using System.Buffers;

namespace ActualLab.Collections;

/// <summary>
/// Extension methods for <see cref="IBufferWriter{T}"/>.
/// </summary>
public static class BufferWriterExt
{
    public static void Write<T>(this IBufferWriter<T> writer, ReadOnlySequence<T> sequence)
    {
        foreach (var segment in sequence)
            writer.Write(segment.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Append<T>(this IBufferWriter<T> writer, ReadOnlySpan<T> span)
    {
        var targetSpan = writer.GetSpan(span.Length);
        span.CopyTo(targetSpan);
        writer.Advance(span.Length);
    }
}
