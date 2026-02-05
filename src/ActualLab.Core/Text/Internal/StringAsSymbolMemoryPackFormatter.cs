namespace ActualLab.Text.Internal;

#if !NETSTANDARD2_0

/// <summary>
/// A MemoryPack formatter for strings that uses the <see cref="Symbol"/> versioned format.
/// </summary>
public sealed class StringAsSymbolMemoryPackFormatter : MemoryPackFormatter<string>
{
    public static readonly StringAsSymbolMemoryPackFormatter Default = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref string? value)
    {
        writer.WriteObjectHeader(1);
        writer.WriteVarInt(writer.GetStringWriteLength(value));
        writer.WriteString(value ?? "");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref MemoryPackReader reader, scoped ref string? value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = "";
            return;
        }

        Span<int> deltas = stackalloc int[count];
        for (int i = 0; i < count; i++)
            deltas[i] = reader.ReadVarIntInt32();

        if (count == 1) {
            value = deltas[0] != 0 ? reader.ReadString() : "";
            return;
        }

        // Something is off, Symbol type should have just 1 delta
        value = "";
        for (int i = 0; i < count; i++)
            reader.Advance(deltas[i]);
    }
}

#endif
