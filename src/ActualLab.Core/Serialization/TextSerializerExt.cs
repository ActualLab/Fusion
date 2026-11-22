using ActualLab.Conversion;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// Extension methods for <see cref="ITextSerializer"/>.
/// </summary>
public static class TextSerializerExt
{
    // Read and Write: they activate serializer.Root and call Read/WriteUnscoped

    public static object? Read(this ITextSerializer serializer, string data, Type type)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data, type);
    }

    public static object? Read(this ITextSerializer serializer, ReadOnlyMemory<char> data, Type type)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data, type);
    }

    public static string Write(this ITextSerializer serializer, object? value, Type type)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.WriteUnscoped(value, type);
    }

    public static void Write(this ITextSerializer serializer, TextWriter textWriter, object? value, Type type)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        serializer.WriteUnscoped(textWriter, value, type);
    }

    public static T Read<T>(this ITextSerializer<T> serializer, string data)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data);
    }

    public static T Read<T>(this ITextSerializer<T> serializer, ReadOnlyMemory<char> data)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data);
    }

    public static string Write<T>(this ITextSerializer<T> serializer, T value)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.WriteUnscoped(value);
    }

    public static void Write<T>(this ITextSerializer<T> serializer, TextWriter textWriter, T value)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        serializer.WriteUnscoped(textWriter, value);
    }

    // ReadDelimited

    public static object? ReadDelimited(
        this ITextSerializer serializer,
        ref ReadOnlyMemory<byte> data,
        Type type,
        byte delimiter)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadDelimitedUnscoped(ref data, type, delimiter);
    }

    public static object? ReadDelimitedUnscoped(
        this ITextSerializer serializer,
        ref ReadOnlyMemory<byte> data,
        Type type,
        byte delimiter)
    {
        object? result;
        var delimiterIndex = data.Span.IndexOf(delimiter);
        if (delimiterIndex < 0) {
            result = serializer.ReadUnscoped(data, type, out _);
            data = default;
        }
        else {
            result = serializer.ReadUnscoped(data[..delimiterIndex], type, out _);
            data = data.Slice(delimiterIndex + 1);
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(this ITextSerializer serializer, string data)
        => (T)serializer.Read(data, typeof(T))!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Write<T>(this ITextSerializer serializer, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => serializer.Write(value, typeof(T));
}
