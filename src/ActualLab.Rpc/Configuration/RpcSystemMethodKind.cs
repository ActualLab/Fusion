namespace ActualLab.Rpc;

#pragma warning disable CA1027 // Mark enums with FlagsAttribute

public enum RpcSystemMethodKind
{
    None = 0,
    Ok = 0x1001,
    Error = 0x0002,
    Cancel = 0x0003,
    Match = 0x0004,
    NotFound = 0x0010,
    OtherNonStreaming = 0x0080,
    Item = 0x1100,
    Batch = 0x1200,
    OtherStreaming = 0x0800,
}

public static class RpcSystemMethodKindExt
{
    extension(RpcSystemMethodKind kind)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPolymorphicResult()
            => ((int)kind & 0x1000) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAnyStreaming()
            => ((int)kind & 0xF00) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAnyNonStreaming()
            => ((int)kind & 0xFF) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCallResultMethod()
            => ((int)kind & 0x3) != 0;
    }
}
