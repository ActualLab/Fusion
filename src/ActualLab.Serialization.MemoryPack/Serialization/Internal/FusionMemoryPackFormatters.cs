using ActualLab.Fusion;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="Session"/>.
/// </summary>
public sealed class SessionMemoryPackFormatter : VersionTolerantMemoryPackFormatter<Session>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Session value)
    {
        writer.WriteValueWithFormatter(StringFormatter, value.Id);
        offsets[0] = writer.WrittenCount;
    }

    protected override Session ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var id = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        return new Session(id!);
    }
}
