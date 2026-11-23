using ActualLab.CommandR;
using ActualLab.CommandR.Operations;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="ServiceCall"/>.
/// </summary>
public sealed class ServiceCallMemoryPackFormatter : VersionTolerantMemoryPackFormatter<ServiceCall>
{
    protected override int MemberCount => 3;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ServiceCall value)
    {
        writer.WriteValue(value.ServiceType);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.MethodName);
        offsets[1] = writer.WrittenCount;
        writer.WriteUnmanagedArray(value.ArgumentData); // ArgumentData serializes the arguments on every read
        offsets[2] = writer.WrittenCount;
    }

    protected override ServiceCall ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var serviceType = deltas[0] != 0 ? reader.ReadValue<TypeRef>() : default;
        var methodName = deltas[1] != 0 ? reader.ReadString() : null;
        var argumentData = deltas[2] != 0 ? reader.ReadUnmanagedArray<byte>() : null;
        return new ServiceCall(serviceType, methodName!, argumentData!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="OperationCompletion"/>.
/// </summary>
public sealed class OperationCompletionMemoryPackFormatter : VersionTolerantMemoryPackFormatter<OperationCompletion>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        OperationCompletion value)
    {
        writer.WriteValueWithFormatter(
            TypeDecoratingUniSerializedMemoryPackFormatter<TypeSchema.Any, ICommand?>.Default, value.SerializedCommand);
        offsets[0] = writer.WrittenCount;
        writer.WriteArray(value.InvalidationCalls);
        offsets[1] = writer.WrittenCount;
    }

    protected override OperationCompletion ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var serializedCommand = deltas[0] != 0
            ? reader.ReadValueWithFormatter<
                IMemoryPackFormatter<TypeDecoratingUniSerialized<TypeSchema.Any, ICommand?>>,
                TypeDecoratingUniSerialized<TypeSchema.Any, ICommand?>>(
                TypeDecoratingUniSerializedMemoryPackFormatter<TypeSchema.Any, ICommand?>.Default)
            : default;
        var invalidationCalls = deltas[1] != 0 ? reader.ReadArray<ServiceCall>() : null;
        return new OperationCompletion(serializedCommand, invalidationCalls!);
    }
}
