using ActualLab.IO;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="FilePath"/>.
/// </summary>
public sealed class FilePathNerdbankConverter : MessagePackConverter<FilePath>
{
    public override FilePath Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in FilePath value, SerializationContext context)
        => writer.Write(value.Value);
}
