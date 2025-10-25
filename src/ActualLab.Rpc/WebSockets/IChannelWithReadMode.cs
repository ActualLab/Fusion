namespace ActualLab.Rpc.WebSockets;

public interface IChannelWithReadMode<out T>
{
    /// <summary>
    /// Read mode to be used on <see cref="ReadAllAsync"/> call.
    /// </summary>
    public ChannelReadMode ReadMode { get; }

    /// <summary>
    /// When possible, returns an unbuffered reader (<see cref="IAsyncEnumerable{T}"/>)
    /// (i.e., when <see cref="ReadMode"/> is <see cref="ChannelReadMode.Unbuffered"/>),
    /// otherwise falls back to the default reader acquired from <see cref="ChannelReader{T}"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Channel reading async enumerable.</returns>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);
}
