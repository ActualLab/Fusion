using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

public static class TextSerializerExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? ReadDelimited(this ITextSerializer serializer, ref ReadOnlyMemory<byte> data, Type type, byte delimiter)
    {
        object? result;
        var delimiterIndex = data.Span.IndexOf(delimiter);
        if (delimiterIndex < 0) {
            result = serializer.Read(data, type);
            data = default;
        }
        else {
            result = serializer.Read(data[..delimiterIndex], type);
            data = data.Slice(delimiterIndex + 1);
        }
        return result;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(this ITextSerializer serializer, string data)
        => (T) serializer.Read(data, typeof(T))!;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Write<T>(this ITextSerializer serializer, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => serializer.Write(value, typeof(T));

    // Convert

    public static ITextSerializer<T> Convert<TInner, T>(
        this ITextSerializer<TInner> serializer,
        BiConverter<T, TInner> converter)
        => new ConvertingTextSerializer<T, TInner>(serializer, converter);
}
