using System.Buffers;
using Stl.Serialization.Internal;

namespace Stl.Serialization;

public class TypeDecoratingByteSerializer : ByteSerializerBase
{
    public static TypeDecoratingByteSerializer Default { get; } =
        new(ByteSerializer.Default);

    public IByteSerializer Serializer { get; }
    public Func<Type, bool> TypeFilter { get; }

    public TypeDecoratingByteSerializer(IByteSerializer serializer, Func<Type, bool>? typeFilter = null)
    {
        Serializer = serializer;
        TypeFilter = typeFilter ?? (_ => true);
    }

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
        if (!TypeFilter(actualType))
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
