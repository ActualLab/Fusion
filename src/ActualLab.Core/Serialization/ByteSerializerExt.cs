using System.Buffers;

namespace ActualLab.Serialization;

/// <summary>
/// Extension methods for <see cref="IByteSerializer"/>.
/// </summary>
public static class ByteSerializerExt
{
    // Activates serializer.Root - code using Read/WriteUnscoped calls it once for a series of calls.
    // Disposing the result restores the previous serializer, so nested serialization
    // (e.g. by a converter) works as expected. For IByteSerializer<T>, use serializer.Root.Activate().
    public static ByteSerializer.Scope Activate(this IByteSerializer serializer)
        => new(serializer.Root);

    // Read and Write: they activate serializer.Root and call Read/WriteUnscoped

    public static object? Read(this IByteSerializer serializer, ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data, type, out readLength);
    }

    public static void Write(this IByteSerializer serializer, IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        serializer.WriteUnscoped(bufferWriter, value, type);
    }

    public static T Read<T>(this IByteSerializer<T> serializer, ReadOnlyMemory<byte> data, out int readLength)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        return serializer.ReadUnscoped(data, out readLength);
    }

    public static void Write<T>(this IByteSerializer<T> serializer, IBufferWriter<byte> bufferWriter, T value)
    {
        using var _ = new ByteSerializer.Scope(serializer.Root);
        serializer.WriteUnscoped(bufferWriter, value);
    }

    // Read (and advance) - with ref data argument

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? Read(this IByteSerializer serializer, ref ReadOnlyMemory<byte> data, Type type)
    {
        var result = serializer.Read(data, type, out var readLength);
        data = data[readLength..];
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? ReadUnscoped(this IByteSerializer serializer, ref ReadOnlyMemory<byte> data, Type type)
    {
        var result = serializer.ReadUnscoped(data, type, out var readLength);
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
        var bufferWriter = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);
        serializer.Write(bufferWriter, value, type);
        return bufferWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<byte> Write<T>(this IByteSerializer<T> serializer, T value)
    {
        var bufferWriter = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);
        serializer.Write(bufferWriter, value);
        return bufferWriter;
    }
}
