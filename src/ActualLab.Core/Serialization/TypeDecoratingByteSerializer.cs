using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

public class TypeDecoratingByteSerializer(IByteSerializer serializer, Func<Type, bool>? typeFilter = null)
    : ByteSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    [field: AllowNull, MaybeNull]
    public static TypeDecoratingByteSerializer Default {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new(ByteSerializer.Default);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    public IByteSerializer Serializer { get; } = serializer;
    public Func<Type, bool> TypeFilter { get; } = typeFilter ?? (_ => true);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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
