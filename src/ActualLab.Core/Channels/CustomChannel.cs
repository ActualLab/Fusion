namespace ActualLab.Channels;

/// <summary>
/// A channel composed from an explicit reader and writer pair.
/// </summary>
public class CustomChannel<TWrite, TRead> : Channel<TWrite, TRead>
{
    public CustomChannel(ChannelReader<TRead> reader, ChannelWriter<TWrite> writer)
    {
        Reader = reader;
        Writer = writer;
    }
}

/// <summary>
/// A channel composed from an explicit reader and writer pair with the same item type.
/// </summary>
public class CustomChannel<T> : Channel<T>
{
    public CustomChannel(ChannelReader<T> reader, ChannelWriter<T> writer)
    {
        Reader = reader;
        Writer = writer;
    }
}
