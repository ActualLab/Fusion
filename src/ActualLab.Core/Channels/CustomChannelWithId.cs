namespace ActualLab.Channels;

/// <summary>
/// A <see cref="CustomChannel{TWrite, TRead}"/> with an associated identifier.
/// </summary>
public class CustomChannelWithId<TId, TWrite, TRead> : Channel<TWrite, TRead>, IHasId<TId>
{
    public TId Id { get; }

    public CustomChannelWithId(TId id, ChannelReader<TRead> reader, ChannelWriter<TWrite> writer)
    {
        Id = id;
        Reader = reader;
        Writer = writer;
    }
}

/// <summary>
/// A <see cref="CustomChannel{T}"/> with an associated identifier.
/// </summary>
public class CustomChannelWithId<TId, T> : Channel<T>, IHasId<TId>
{
    public TId Id { get; }

    public CustomChannelWithId(TId id, ChannelReader<T> reader, ChannelWriter<T> writer)
    {
        Id = id;
        Reader = reader;
        Writer = writer;
    }
}
