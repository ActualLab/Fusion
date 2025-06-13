using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume you know serialization may involve reflection and dynamic invocations")]
public class TypeDecoratingByteSerializer(IByteSerializer serializer, Func<Type, bool>? typeFilter = null)
    : ByteSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile TypeDecoratingByteSerializer? _default;

    public static TypeDecoratingByteSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(ByteSerializer.Default);
        }
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public IByteSerializer Serializer { get; } = serializer;
    public Func<Type, bool> TypeFilter { get; } = typeFilter ?? (_ => true);

    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var actualTypeRef = Serializer.Read<TypeRef>(data, out var typeRefLength);
        if (actualTypeRef == default) {
            readLength = typeRefLength;
            return null;
        }

        var actualType = actualTypeRef.Resolve();
        if (!type.IsAssignableFrom(actualType))
            throw Errors.UnsupportedSerializedType(actualType);
        if (!TypeFilter.Invoke(actualType))
            throw Errors.UnsupportedSerializedType(actualType);

        var unreadData = data[typeRefLength..];
        var result = Serializer.Read(unreadData, actualType, out readLength);
        readLength += typeRefLength;
        return result;
    }

    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        if (ReferenceEquals(value, null)) {
            Serializer.Write(bufferWriter, default(TypeRef));
            return;
        }

        var actualType = value.GetType();
        var actualTypeRef = new TypeRef(actualType).WithoutAssemblyVersions();
        Serializer.Write(bufferWriter, actualTypeRef);
        Serializer.Write(bufferWriter, value, actualType);
    }
}
