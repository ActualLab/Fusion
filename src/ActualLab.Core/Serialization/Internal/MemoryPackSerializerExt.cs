namespace ActualLab.Serialization.Internal;

public static class MemoryPackSerializerExt
{
#if !NETSTANDARD2_0

    private static readonly Func<object?, object?> StateGetter;
    private static readonly Action<object?, object?> StateSetter;
    private static readonly Func<object?, MemoryPackReaderOptionalState?> ReaderStateGetter;
    private static readonly Action<object?, MemoryPackReaderOptionalState?> ReaderStateSetter;
    private static readonly Func<object?, MemoryPackWriterOptionalState?> WriterStateGetter;
    private static readonly Action<object?, MemoryPackWriterOptionalState?> WriterStateSetter;

    static MemoryPackSerializerExt()
    {
        var bfStaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        var tSerializer = typeof(MemoryPackSerializer);
        var fState = tSerializer.GetField("threadStaticState", bfStaticNonPublic)!;
        var fReaderState = tSerializer.GetField("threadStaticReaderOptionalState", bfStaticNonPublic)!;
        var fWriterState = tSerializer.GetField("threadStaticWriterOptionalState", bfStaticNonPublic)!;
        StateGetter = fState.GetGetter();
        StateSetter = fState.GetSetter();
        ReaderStateGetter = fReaderState.GetGetter<MemoryPackReaderOptionalState?>();
        ReaderStateSetter = fReaderState.GetSetter<MemoryPackReaderOptionalState?>();
        WriterStateGetter = fWriterState.GetGetter<MemoryPackWriterOptionalState?>();
        WriterStateSetter = fWriterState.GetSetter<MemoryPackWriterOptionalState?>();
    }

    public static ReaderStateSnapshot ReaderState {
        get => new(ReaderStateGetter.Invoke(null));
        set => ReaderStateSetter.Invoke(null, value.ReaderState);
    }

    public static WriterStateSnapshot WriterState {
        get => new(WriterStateGetter.Invoke(null), StateGetter.Invoke(null));
        set {
            StateSetter.Invoke(null, value.State);
            WriterStateSetter.Invoke(null, value.WriterState);
        }
    }

    // Nested types

    public readonly record struct ReaderStateSnapshot(MemoryPackReaderOptionalState? ReaderState);
    public readonly record struct WriterStateSnapshot(MemoryPackWriterOptionalState? WriterState, object? State);

#else

    public static ReaderStateSnapshot ReaderState {
        get => default;
        // ReSharper disable once ValueParameterNotUsed
        set { }
    }

    public static WriterStateSnapshot WriterState {
        get => default;
        // ReSharper disable once ValueParameterNotUsed
        set { }
    }

    // Nested types

    public readonly record struct ReaderStateSnapshot;
    public readonly record struct WriterStateSnapshot;

#endif
}
