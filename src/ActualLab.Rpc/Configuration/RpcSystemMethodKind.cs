namespace ActualLab.Rpc;

#pragma warning disable CA1027 // Mark enums with FlagsAttribute

/// <summary>
/// Defines the kind of a system RPC method (Ok, Error, Cancel, streaming, etc.).
/// </summary>
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

/// <summary>
/// Extension methods for <see cref="RpcSystemMethodKind"/>.
/// </summary>
public static class RpcSystemMethodKindExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPolymorphicResult(this RpcSystemMethodKind kind)
        => ((int)kind & 0x1000) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAnyStreaming(this RpcSystemMethodKind kind)
        => ((int)kind & 0xF00) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAnyNonStreaming(this RpcSystemMethodKind kind)
        => ((int)kind & 0xFF) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCallResultMethod(this RpcSystemMethodKind kind)
        => ((int)kind & 0x3) != 0;
}
