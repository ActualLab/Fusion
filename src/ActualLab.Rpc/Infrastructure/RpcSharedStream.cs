using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable MA0042

public abstract class RpcSharedStream(RpcStream stream) : WorkerBase, IRpcSharedObject
{
#pragma warning disable CA2201
    protected static readonly Exception NoMoreItemTag = new();
#pragma warning restore CA2201

    private ILogger? _log;
    private long _lastKeepAliveAt = CpuTimestamp.Now.Value;

    protected ILogger Log => _log ??= Peer.Hub.Services.LogFor(GetType());

    public RpcObjectId Id { get; } = stream.Id;
    public RpcObjectKind Kind { get; } = stream.Kind;
    public RpcStream Stream { get; } = stream;
    public RpcPeer Peer { get; } = stream.Peer!;
    public CpuTimestamp LastKeepAliveAt {
        get => new(Interlocked.Read(ref _lastKeepAliveAt));
        set => Interlocked.Exchange(ref _lastKeepAliveAt, value.Value);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    Task IRpcObject.Reconnect(CancellationToken cancellationToken)
        => throw ActualLab.Internal.Errors.InternalError(
            $"This method should never be called on {nameof(RpcSharedStream)}.");

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    void IRpcObject.Disconnect()
        => throw ActualLab.Internal.Errors.InternalError(
            $"This method should never be called on {nameof(RpcSharedStream)}.");

    public void KeepAlive()
        => LastKeepAliveAt = CpuTimestamp.Now;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract Task OnAck(long nextIndex, Guid hostId);
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

    protected override async Task DisposeAsyncCore()
    {
        _acks.Writer.TryWrite((long.MaxValue, false)); // Just in case
        try {
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
        finally {
            Peer.SharedObjects.Unregister(this);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override Task OnAck(long nextIndex, Guid hostId)
    {
        var mustReset = hostId != default;
        if (mustReset && !Equals(Stream.Id.HostId, hostId))
            return SendMissing();

        LastKeepAliveAt = CpuTimestamp.Now;
        lock (Lock) {
            var whenRunning = WhenRunning;
            if (whenRunning == null) {
                if (mustReset && nextIndex == 0)
                    this.Start();
                else
                    return SendMissing();
            }
            else if (whenRunning.IsCompleted)
                return SendMissing();

            _acks.Writer.TryWrite((nextIndex, mustReset)); // Must always succeed for unbounded channel
            return Task.CompletedTask;
        }
    }

    // Protected & private methods

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    protected override async Task OnRun(CancellationToken cancellationToken)
#pragma warning restore IL2046
    {
        IAsyncEnumerator<T>? enumerator = null;
        try {
            enumerator = Stream.GetLocalSource().GetAsyncEnumerator(cancellationToken);
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
                await _batcher.Flush(index).ConfigureAwait(false);
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
                    await SendInvalidPosition(index).ConfigureAwait(false);
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
                                item = Result.Error<T>(NoMoreItemTag);
                                isFullyBuffered = true;
                            }
                        }
                        catch (Exception e) {
                            item = Result.Error<T>(e.IsCancellationOf(cancellationToken)
                                ? Errors.RpcStreamNotFound()
                                : e);
                            isFullyBuffered = true;
                        }
                        buffer.PushTail(item);
                    }

                    // 3.2. Add all buffered items to batcher
                    while (index < maxIndex && bufferIndex < buffer.Count) {
                        item = buffer[bufferIndex++];
                        await _batcher.Add(index++, item).ConfigureAwait(false);
                        if (item.HasError) {
                            // It's the last item -> all we can do now is to wait for Ack;
                            // Note that Batcher.Add automatically flushes on error.
                            goto nextAck;
                        }
                    }
                    if (isFullyBuffered)
                        goto nextAck;
                    if (whenMovedNextAsTask == null)
                        continue;

                    // 3.3. Flush & await whenMovedNextAsTask or whenAckReady
                    await _batcher.Flush(index).ConfigureAwait(false);
                    var completedTask = await Task
                        .WhenAny(whenAckReady, whenMovedNextAsTask)
                        .ConfigureAwait(false);
                    if (completedTask == whenAckReady)
                        goto nextAck; // Got Ack, must restart
                }
            }
        }
        finally {
            _ = DisposeAsync();
            _ = enumerator?.DisposeAsync();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private Task SendMissing()
        => _systemCallSender.Disconnect(Peer, [Id.LocalId]);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private Task SendInvalidPosition(long index)
        => Send(index, Result.Error<T>(Errors.RpcStreamInvalidPosition()));

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private Task Send(long index, Result<T> item)
    {
        // Debug.WriteLine($"{Id}: <- #{index} (ack @ {ackIndex})");
        if (item.IsValue(out var value))
            return _systemCallSender.Item(Peer, Id.LocalId, index, value);

        var error = ReferenceEquals(item.Error, NoMoreItemTag) ? null : item.Error;
        return _systemCallSender.End(Peer, Id.LocalId, index, error);
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

    private sealed class Batcher(RpcSharedStream<T> stream)
    {
        private const int BatchSize = 256;

        private readonly bool _isPolymorphic = !(typeof(T).IsValueType || typeof(T).IsSealed);
        private readonly List<T> _items = new(BatchSize / 4); // Our chance to fully fill the batch is low
        private Type? _itemType;

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public async ValueTask Add(long index, Result<T> item)
        {
            if (!item.IsValue(out var vItem)) {
                await Flush(index).ConfigureAwait(false);
                await stream.Send(index, item).ConfigureAwait(false);
                return;
            }

            if (_isPolymorphic) {
                var itemType = vItem?.GetType();
                if (_items.Count >= BatchSize || (itemType != null && itemType != _itemType))
                    await Flush(index).ConfigureAwait(false);
                _itemType ??= itemType;
            }
            else if (_items.Count >= BatchSize)
                await Flush(index).ConfigureAwait(false);

            _items.Add(item);
        }

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public Task Flush(long nextIndex)
        {
            var count = _items.Count;
            if (count == 0)
                return Task.CompletedTask;

            if (count == 1) {
                var result = stream.Send(nextIndex - count, _items[0]);
                _items.Clear();
                _itemType = null;
                return result;
            }

            {
                var items = _isPolymorphic
                    ? (T[])Array.CreateInstance(_itemType ?? typeof(T), count)
                    : new T[count];
                _items.CopyTo(items);
                var result = stream._systemCallSender.Batch(stream.Peer, stream.Id.LocalId, nextIndex - count, items);
                _items.Clear();
                _itemType = null;
                return result;
            }
        }
    }
}
