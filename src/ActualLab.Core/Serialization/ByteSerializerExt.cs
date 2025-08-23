using ActualLab.Conversion;
using ActualLab.IO;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

public static class ByteSerializerExt
{
    // Read (and advance) - with ref data argument

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? Read(this IByteSerializer serializer, ref ReadOnlyMemory<byte> data, Type type)
    {
        var result = serializer.Read(data, type, out var readLength);
        data = data[readLength..];
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(this IByteSerializer serializer, ref ReadOnlyMemory<byte> data)
        where T : class // Avoid generic methods w/ struct params + boxing
    {
        var result = (T)serializer.Read(data, typeof(T), out var readLength)!;
        data = data[readLength..];
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(this IByteSerializer<T> serializer, ref ReadOnlyMemory<byte> data)
        where T : class // Avoid generic methods w/ struct params + boxing
    {
        var result = serializer.Read(data, out var readLength)!;
        data = data[readLength..];
        return result;
    }

    // Write w/o IBufferWriter<byte> argument

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<byte> Write<T>(this IByteSerializer serializer, T value)
        where T : class // Avoid generic methods w/ struct params + boxing
        => serializer.Write(value, typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<byte> Write(this IByteSerializer serializer, object? value, Type type)
    {
        var bufferWriter = new ArrayPoolBuffer<byte>(false);
        serializer.Write(bufferWriter, value, type);
        return bufferWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<byte> Write<T>(this IByteSerializer<T> serializer, T value)
    {
        var bufferWriter = new ArrayPoolBuffer<byte>(false);
        serializer.Write(bufferWriter, value);
        return bufferWriter;
    }

    // Convert

    public static IByteSerializer<T> Convert<TInner, T>(
        this IByteSerializer<TInner> serializer,
        BiConverter<T, TInner> converter)
        => new ConvertingByteSerializer<T, TInner>(serializer, converter);
}
