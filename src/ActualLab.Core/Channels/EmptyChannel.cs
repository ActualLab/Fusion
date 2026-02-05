namespace ActualLab.Channels;

/// <summary>
/// Marker interface for channels that are immediately completed with no items.
/// </summary>
public interface IEmptyChannel;

/// <summary>
/// A channel that is immediately completed and produces no items.
/// </summary>
public class EmptyChannel<T> : Channel<T, T>, IEmptyChannel
{
    public static readonly EmptyChannel<T> Instance = new();

    private EmptyChannel()
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() {
            SingleReader = false,
            SingleWriter = false,
        });
        channel.Writer.TryComplete();
        Reader = channel.Reader;
        Writer = channel.Writer;
    }
}
