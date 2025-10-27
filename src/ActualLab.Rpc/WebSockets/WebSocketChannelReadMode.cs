namespace ActualLab.Rpc.WebSockets;

public enum WebSocketChannelReadMode
{
    /// <summary>
    /// <see cref="Channel{T}.Reader"/> is unavailable (returns <c>null</c>),
    /// <see cref="WebSocketChannel{T}.GetAsyncEnumerator"/> reads directly from the underlying data source.
    /// </summary>
    Unbuffered = 0,
    /// <summary>
    /// <see cref="Channel{T}.Reader"/> is available,
    /// <see cref="WebSocketChannel{T}.GetAsyncEnumerator"/> uses <see cref="ChannelReader{T}.ReadAllAsync"/>,
    /// i.e., reads from the reader acting as a buffer.
    /// </summary>
    Buffered = 1,
}
