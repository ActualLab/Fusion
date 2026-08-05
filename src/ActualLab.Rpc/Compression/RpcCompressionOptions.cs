namespace ActualLab.Rpc.Compression;

/// <summary>
/// The policy an <see cref="RpcCompressionFormat"/>'s outbound codec follows: what's worth
/// compressing, and how long the compression context may live.
/// </summary>
public sealed record RpcCompressionOptions
{
    public static RpcCompressionOptions Default { get; set; } = new();

    // Below this size a frame costs more in flush marker than the codec saves on it
    public int MinCompressedFrameSize { get; init; } = 512;
    // Resetting the dictionary periodically bounds both the memory a connection pins and how far
    // a BREACH-style probe can correlate across frames. It does NOT make compressing a secret
    // alongside attacker-controlled data in one frame safe - nothing here can.
    public long ContextResetBytes { get; init; } = 128 * 1024;
    public int ContextResetFrames { get; init; } = 512;
}
