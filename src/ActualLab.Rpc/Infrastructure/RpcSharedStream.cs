using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable MA0042

public abstract class RpcSharedStream(RpcStream stream) : WorkerBase, IRpcSharedObject
{
#pragma warning disable CA2201
    // We never throw this error, so it's fine to share its single instance here
    protected static readonly Exception NoMoreItemsTag = new();
#pragma warning restore CA2201

    private long _lastKeepAliveAt = CpuTimestamp.Now.Value;

    protected ILogger Log => field ??= Peer.Hub.Services.LogFor(GetType());

    public RpcObjectId Id { get; } = stream.Id;
    public RpcObjectKind Kind { get; } = stream.Kind;
    public RpcStream Stream { get; } = stream;
    public RpcPeer Peer { get; } = stream.Peer!;
    public CpuTimestamp LastKeepAliveAt {
        get => new(Interlocked.Read(ref _lastKeepAliveAt));
        set => Interlocked.Exchange(ref _lastKeepAliveAt, value.Value);
    }

    Task IRpcObject.Reconnect(CancellationToken cancellationToken)
        => throw ActualLab.Internal.Errors.InternalError(
            $"This method should never be called on {nameof(RpcSharedStream)}.");

    void IRpcObject.Disconnect()
        => throw ActualLab.Internal.Errors.InternalError(
            $"This method should never be called on {nameof(RpcSharedStream)}.");

    public void KeepAlive()
        => LastKeepAliveAt = CpuTimestamp.Now;

    public abstract void OnAck(long nextIndex, Guid hostId);
}

public sealed class RpcSharedStream<T> : RpcSharedStream
{
    private readonly RpcSystemCallSender _systemCallSender;
    private readonly Channel<(long NextIndex, bool MustReset)> _acks = Channel.CreateUnbounded<(long, bool)>(
        new() {
            SingleReader = true,
            SingleWriter = true,
        });
    private readonly Batcher _batcher;

    public RpcSharedStream(RpcStream stream) : base(stream)
    {
        _systemCallSender = stream.Peer!.Hub.SystemCallSender;
        Stream = (RpcStream<T>)stream;
        _batcher = new(this);
    }

    public new RpcStream<T> Stream { get; }

    public override void OnAck(long nextIndex, Guid hostId)
    {
        var mustReset = hostId != default;
        if (mustReset && !Equals(Stream.Id.HostId, hostId)) {
            SendMissing();
            return;
        }

        LastKeepAliveAt = CpuTimestamp.Now;
        lock (Lock) {
            var whenRunning = WhenRunning;
            if (whenRunning is null) {
                if (mustReset && nextIndex == 0)
                    this.Start();
                else {
                    SendMissing();
                    return;
                }
            }
            else if (whenRunning.IsCompleted) {
                SendMissing();
                return;
            }

            _acks.Writer.TryWrite((nextIndex, mustReset)); // Must always succeed for unbounded channel
        }
    }

    // Protected & private methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var enumerator = Stream.GetLocalSource().GetAsyncEnumerator(cancellationToken);
        await using var _ = enumerator.ConfigureAwait(false);

        var isFullyBuffered = false;
        var ackReader = _acks.Reader;
        var buffer = new RingBuffer<Result<T>>(Stream.AckAdvance + 1);
        var bufferStart = 0L;
        var index = 0L;
        var whenAckReady = ackReader.WaitToReadAsync(cancellationToken).AsTask();
        var whenMovedNext = SafeMoveNext(enumerator);
        var whenMovedNextAsTask = (Task<bool>?)null;
        while (true) {
            nextAck:
            // 1. Await for an acknowledgement & process accumulated acknowledgements
            _batcher.Flush(index);
            (long NextIndex, bool MustReset) ack = (-1L, false);
            if (!whenAckReady.IsCompleted) {
                // Debug.WriteLine($"{Id}: ?ACK");
                await whenAckReady.ConfigureAwait(false);
            }
            while (ackReader.TryRead(out var nextAck)) {
                ack = nextAck;
                // Debug.WriteLine($"{Id}: +ACK: {ack}");
                if (ack.NextIndex == long.MaxValue)
                    return; // Client tells us it's done w/ this stream

                if (ack.MustReset || index < ack.NextIndex)
                    index = ack.NextIndex;
            }
            if (ack.NextIndex < 0) {
                Log.LogWarning("Something is off: couldn't read an acknowledgement");
                return;
            }
            whenAckReady = ackReader.WaitToReadAsync(cancellationToken).AsTask();
            if (whenAckReady.IsCompleted)
                goto nextAck;

            // 2. Remove what's useless from buffer
            {
                var bufferShift = (int)(ack.NextIndex - bufferStart).Clamp(0, buffer.Count);
                buffer.MoveHead(bufferShift);
                bufferStart += bufferShift;
            }

            // 3. Recalculate the next range to send
            if (index < bufferStart) {
                // The requested item is somewhere before the buffer start position
                SendInvalidPosition(index);
                goto nextAck;
            }
            var bufferIndex = (int)(index - bufferStart);

            // 3. Send as much as we can
            var maxIndex = ack.NextIndex + Stream.AckAdvance;
            while (index < maxIndex) {
                Result<T> item;

                // 3.1. Buffer as much as we can
                while (buffer.HasRemainingCapacity && !isFullyBuffered) {
                    if (!whenMovedNext.IsCompleted) {
                        // Both tasks aren't completed yet.
                        whenMovedNextAsTask ??= whenMovedNext.AsTask();
                        break;
                    }

                    try {
                        if (whenMovedNext.Result) {
                            item = enumerator.Current;
                            whenMovedNext = SafeMoveNext(enumerator);
                            whenMovedNextAsTask = null; // Must go after SafeMoveNext call (which may fail)
                        }
                        else {
                            item = Result.NewError<T>(NoMoreItemsTag);
                            isFullyBuffered = true;
                        }
                    }
                    catch (Exception e) {
                        item = Result.NewError<T>(e.IsCancellationOf(cancellationToken)
                            ? Errors.RpcStreamNotFound()
                            : e);
                        isFullyBuffered = true;
                    }
                    buffer.PushTail(item);
                }

                // 3.2. Add all buffered items to the batcher
                while (index < maxIndex && bufferIndex < buffer.Count) {
                    item = buffer[bufferIndex++];
                    _batcher.Add(index++, item);
                    if (item.HasError) {
                        // It's the last item -> all we can do now is to wait for Ack;
                        // Note that Batcher.Add automatically flushes on error.
                        goto nextAck;
                    }
                }
                if (isFullyBuffered)
                    goto nextAck;
                if (whenMovedNextAsTask is null)
                    continue;

                // 3.3. Flush & await whenMovedNextAsTask or whenAckReady
                _batcher.Flush(index);
                var completedTask = await Task
                    .WhenAny(whenAckReady, whenMovedNextAsTask)
                    .ConfigureAwait(false);
                if (completedTask == whenAckReady)
                    goto nextAck; // Got Ack, must restart
            }
        }
    }

    protected override Task OnStop()
    {
        try {
            _acks.Writer.TryWrite((long.MaxValue, false)); // Just in case
        }
        finally {
            Peer.SharedObjects.Unregister(this);
        }
        return Task.CompletedTask;
    }

    private void SendMissing()
        => _systemCallSender.Disconnect(Peer, [Id.LocalId]);

    private void SendInvalidPosition(long index)
        => Send(index, Result.NewError<T>(Errors.RpcStreamInvalidPosition()));

    private void Send(long index, Result<T> item)
    {
        // Debug.WriteLine($"{Id}: <- #{index} (ack @ {ackIndex})");
        var (value, error) = item;
        if (error is null) {
            _systemCallSender.Item(Peer, Id.LocalId, index, value);
            return;
        }

        if (ReferenceEquals(item.Error, NoMoreItemsTag))
            error = null;
        _systemCallSender.End(Peer, Id.LocalId, index, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<bool> SafeMoveNext(IAsyncEnumerator<T> enumerator)
    {
        try {
            return enumerator.MoveNextAsync();
        }
        catch (Exception e) {
            return ValueTaskExt.FromException<bool>(e);
        }
    }

    // Nested types

    private sealed class Batcher
    {

        private readonly int _batchSize;
        private readonly bool _isPolymorphic = !(typeof(T).IsValueType || typeof(T).IsSealed);
        private readonly List<T> _items;
        private Type? _itemType;
        private readonly RpcSharedStream<T> _stream;

        public Batcher(RpcSharedStream<T> stream)
        {
            _batchSize = stream.Stream.BatchSize.Clamp(1, RpcStream.MaxBatchSize);
            _stream = stream;
            _items = new List<T>(capacity: Math.Max(1, _batchSize / 4));
        }

        public void Add(long index, Result<T> item)
        {
            var (value, error) = item;
            if (error is not null) {
                Flush(index);
                _stream.Send(index, item);
                return;
            }

            if (_isPolymorphic) {
                var itemType = value?.GetType();
                if (_items.Count >= _batchSize || (itemType is not null && itemType != _itemType))
                    Flush(index);
                _itemType ??= itemType;
            }
            else if (_items.Count >= _batchSize)
                Flush(index);

            _items.Add(item);
        }

        public void Flush(long nextIndex)
        {
            var count = _items.Count;
            if (count == 0)
                return;

            if (count == 1) {
                _stream.Send(nextIndex - count, _items[0]);
                _items.Clear();
                _itemType = null;
                return;
            }

            {
                var items = _isPolymorphic
                    ? (T[])Array.CreateInstance(_itemType ?? typeof(T), count)
                    : new T[count];
                _items.CopyTo(items);
                _stream._systemCallSender.Batch(
                    _stream.Peer, _stream.Id.LocalId, nextIndex - count, items);
                _items.Clear();
                _itemType = null;
            }
        }
    }
}
