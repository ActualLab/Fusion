namespace ActualLab.Channels;

public class NullChannel<T> : Channel<T>
{
    public static readonly NullChannel<T> Instance = new();

    private sealed class NullChannelReader : ChannelReader<T>
    {
        internal NullChannelReader()
        { }

        public override Task Completion
            => TaskExt.NewNeverEndingUnreferenced();

        public override bool TryRead(out T item)
        {
            item = default!;
            return false;
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTaskExt.FalseTask;
        }
    }

    private sealed class NullChannelWriter : ChannelWriter<T>
    {
        internal NullChannelWriter()
        { }

        public override bool TryComplete(Exception? error = null)
            => false;

        public override bool TryWrite(T item)
            => true;

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTaskExt.TrueTask;
        }
    }

    private NullChannel()
    {
        Reader = new NullChannelReader();
        Writer = new NullChannelWriter();
    }
}
