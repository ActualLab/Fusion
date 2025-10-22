namespace ActualLab.Rpc.WebSockets;

public interface IChannelWithReadAllUnbuffered<out T>
{
    public bool UseReadAllUnbuffered { get; }
    public IAsyncEnumerable<T> ReadAllUnbuffered(CancellationToken cancellationToken = default);
}
