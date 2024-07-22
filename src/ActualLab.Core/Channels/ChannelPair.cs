namespace ActualLab.Channels;

public class ChannelPair<T>(Channel<T> channel1, Channel<T> channel2)
{
    public static readonly ChannelPair<T> Null = new(NullChannel<T>.Instance, NullChannel<T>.Instance);

    public Channel<T> Channel1 { get; protected init; } = channel1;
    public Channel<T> Channel2 { get; protected init; } = channel2;

    protected ChannelPair() : this(null!, null!) { }
}

public static class ChannelPair
{
    // Create

    public static ChannelPair<T> Create<T>(Channel<T> channel1, Channel<T> channel2)
        => new(channel1, channel2);

    public static ChannelPair<T> Create<T>(BoundedChannelOptions boundedChannelOptions)
    {
        var channel1 = Channel.CreateBounded<T>(boundedChannelOptions);
        var channel2 = Channel.CreateBounded<T>(boundedChannelOptions);
        return Create(channel1, channel2);
    }

    public static ChannelPair<T> Create<T>(UnboundedChannelOptions unboundedChannelOptions)
    {
        var channel1 = Channel.CreateUnbounded<T>(unboundedChannelOptions);
        var channel2 = Channel.CreateUnbounded<T>(unboundedChannelOptions);
        return Create(channel1, channel2);
    }

    public static ChannelPair<T> Create<T>(ChannelOptions channelOptions) =>
        channelOptions switch {
            BoundedChannelOptions o => Create<T>(o),
            UnboundedChannelOptions o => Create<T>(o),
            _ => throw new ArgumentOutOfRangeException(nameof(channelOptions)),
        };

    // CreateTwisted

    public static ChannelPair<T> CreateTwisted<T>(Channel<T> channel1, Channel<T> channel2)
        => new(
            new CustomChannel<T>(channel1.Reader, channel2.Writer),
            new CustomChannel<T>(channel2.Reader, channel1.Writer));

    public static ChannelPair<T> CreateTwisted<T>(BoundedChannelOptions boundedChannelOptions)
    {
        var channel1 = Channel.CreateBounded<T>(boundedChannelOptions);
        var channel2 = Channel.CreateBounded<T>(boundedChannelOptions);
        return CreateTwisted(channel1, channel2);
    }

    public static ChannelPair<T> CreateTwisted<T>(UnboundedChannelOptions unboundedChannelOptions)
    {
        var channel1 = Channel.CreateUnbounded<T>(unboundedChannelOptions);
        var channel2 = Channel.CreateUnbounded<T>(unboundedChannelOptions);
        return CreateTwisted(channel1, channel2);
    }

    public static ChannelPair<T> CreateTwisted<T>(ChannelOptions channelOptions) =>
        channelOptions switch {
            BoundedChannelOptions o => CreateTwisted<T>(o),
            UnboundedChannelOptions o => CreateTwisted<T>(o),
            _ => throw new ArgumentOutOfRangeException(nameof(channelOptions)),
        };
}
