using System.Diagnostics;
using Stl.Interception;
using Stl.Internal;
using Stl.Rpc.Infrastructure;

namespace Stl.Rpc;

#pragma warning disable MA0055

[DataContract]
public abstract partial class RpcStream : IRpcObject
{
    protected static readonly ConcurrentDictionary<object, Unit> ActiveObjects = new();
    protected static readonly UnboundedChannelOptions RemoteChannelOptions = new() {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false, // We don't want sync handlers to "clog" the call processing loop
    };

    [DataMember, MemoryPackOrder(1)]
    public int AckDistance { get; init; } = 30;
    [DataMember, MemoryPackOrder(2)]
    public int AdvanceDistance { get; init; } = 61;

    // Non-serialized members
    [JsonIgnore, MemoryPackIgnore] public long Id { get; protected set; }
    [JsonIgnore, MemoryPackIgnore] public RpcPeer? Peer { get; protected set; }
    [JsonIgnore, MemoryPackIgnore] public abstract Type ItemType { get; }
    [JsonIgnore, MemoryPackIgnore] public abstract RpcObjectKind Kind { get; }

    public static RpcStream<T> New<T>(IAsyncEnumerable<T> outgoingSource)
        => new(outgoingSource);
    public static RpcStream<T> New<T>(IEnumerable<T> outgoingSource)
        => new(outgoingSource.ToAsyncEnumerable());

    public override string ToString()
        => $"{GetType().GetName()}(#{Id} @ {Peer?.Ref}, {Kind})";

    Task IRpcObject.OnReconnected(CancellationToken cancellationToken)
        => OnReconnected(cancellationToken);

    void IRpcObject.OnMissing()
        => OnMissing();

    // Protected methods

    protected internal abstract ArgumentList CreateStreamItemArguments();
    protected internal abstract Task OnItem(long index, long ackIndex, object? item);
    protected internal abstract Task OnEnd(long index, Exception? error);
    protected abstract Task OnReconnected(CancellationToken cancellationToken);
    protected abstract void OnMissing();
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial class RpcStream<T> : RpcStream, IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<T>? _localSource;
    private Channel<T>? _remoteChannel;
    private long _nextIndex;
    private bool _isRemoteObjectRegistered;
    private bool _isMissing;
    private readonly object _lock;

    [DataMember, MemoryPackOrder(0)]
    public long SerializedId {
        get {
            // This member must be never accessed directly - its only purpose is to be called on serialization
            this.RequireKind(RpcObjectKind.Local);
            if (Id > 0) // Already registered
                return Id;

            Peer = RpcOutboundContext.Current?.Peer ?? RpcInboundContext.GetCurrent().Peer;
            var sharedObjects = Peer.SharedObjects;
            Id = sharedObjects.NextId(); // NOTE: Id changes on serialization!
            var sharedStream = new RpcSharedStream<T>(this);
            sharedObjects.Register(sharedStream);
            return Id;
        }
        set {
            lock (_lock) {
                this.RequireKind(RpcObjectKind.Remote);
                Id = value;
                Peer = RpcInboundContext.GetCurrent().Peer;
                _isRemoteObjectRegistered = true;
                Peer.RemoteObjects.Register(this);
            }
        }
    }

    [JsonIgnore] public override Type ItemType => typeof(T);
    [JsonIgnore] public override RpcObjectKind Kind
        => _localSource != null ? RpcObjectKind.Local : RpcObjectKind.Remote;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public RpcStream()
        => _lock = new();

    public RpcStream(IAsyncEnumerable<T> localSource)
    {
        _localSource = localSource;
        _lock = null!; // Must not be used for local streams
    }

    ~RpcStream()
    {
        if (_lock != null!)
            Close(Errors.AlreadyDisposed(GetType()));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_localSource != null)
            return _localSource.GetAsyncEnumerator(cancellationToken);

        lock (_lock) {
            if (_remoteChannel != null)
                throw Internal.Errors.RemoteRpcStreamCanBeEnumeratedJustOnce();

            _remoteChannel = Channel.CreateUnbounded<T>(RemoteChannelOptions);
            if (_nextIndex == long.MaxValue) // Marked as missing
                _remoteChannel.Writer.TryComplete(Internal.Errors.RpcStreamNotFound());
            return new RemoteChannelEnumerator(this, cancellationToken);
        }
    }

    // Protected methods

    internal IAsyncEnumerable<T> GetLocalSource()
    {
        this.RequireKind(RpcObjectKind.Local);
        return _localSource!;
    }

    protected internal override ArgumentList CreateStreamItemArguments()
        => ArgumentList.New<long, int, T>(0L, 0, default!);

    protected internal override Task OnItem(long index, long ackIndex, object? item)
    {
        lock (_lock) {
            if (_remoteChannel == null || index < _nextIndex)
                return Task.CompletedTask;

            if (index > _nextIndex)
                return SendAckFromLock(_nextIndex, true);

            // Debug.WriteLine($"{Id}: +#{index} (ack @ {ackIndex})");
            _nextIndex++;
            _remoteChannel.Writer.TryWrite((T)item!); // Must always succeed for unbounded channel
            var delta = _nextIndex - ackIndex;
            if (delta >= 0 && delta % AckDistance == 0)
                return SendAckFromLock(_nextIndex);

            return Task.CompletedTask;
        }
    }

    protected internal override Task OnEnd(long index, Exception? error)
    {
        lock (_lock) {
            if (_remoteChannel == null || index < _nextIndex)
                return Task.CompletedTask;

            if (index > _nextIndex)
                return SendAckFromLock(_nextIndex, true);

            // Debug.WriteLine($"{Id}: +{index} (ended!)");
            CloseFromLock(error);
            return Task.CompletedTask;
        }
    }

    protected override Task OnReconnected(CancellationToken cancellationToken)
    {
        lock (_lock)
            return _remoteChannel != null ? SendAckFromLock(_nextIndex, true) : Task.CompletedTask;
    }

    protected override void OnMissing()
    {
        lock (_lock) {
            if (_isMissing)
                return;

            _isMissing = true;
            CloseFromLock(Internal.Errors.RpcStreamNotFound());
        }
    }

    // Private methods

    private void Close(Exception? error)
    {
        lock (_lock)
            CloseFromLock(error);
    }

    private void CloseFromLock(Exception? error)
    {
        if (_remoteChannel != null) {
            if (_nextIndex != long.MaxValue)
                _ = SendCloseFromLock();
            _remoteChannel.Writer.TryComplete(error);
        }
        if (_isRemoteObjectRegistered) {
            _isRemoteObjectRegistered = false;
            Peer?.RemoteObjects.Unregister(this);
        }
    }

    private Task SendCloseFromLock()
    {
        _nextIndex = int.MaxValue;
        return SendAckFromLock(_nextIndex);
    }

    private Task SendAckFromLock(long index, bool mustReset = false)
    {
        // Debug.WriteLine($"{Id}: <- ACK: ({index}, {mustReset})");
        return !_isMissing
            ? Peer!.Hub.SystemCallSender.Ack(Peer, Id, index, mustReset)
            : Task.CompletedTask;
    }

    // Nested types

    private class RemoteChannelEnumerator : IAsyncEnumerator<T>
    {
        private readonly ChannelReader<T> _reader;
        private bool _isStarted;
        private bool _isEnded;
        private Result<T> _current;
        private readonly RpcStream<T> _stream;
        private readonly CancellationToken _cancellationToken;

        public RemoteChannelEnumerator(RpcStream<T> stream, CancellationToken cancellationToken)
        {
            _stream = stream;
            _cancellationToken = cancellationToken;
            _reader = stream._remoteChannel!.Reader;
            ActiveObjects.TryAdd(this, default);
        }

        public T Current => _isStarted
            ? _current.Value
            : throw new InvalidOperationException($"{nameof(MoveNextAsync)} should be called first.");

        public ValueTask DisposeAsync()
        {
            _stream.Close(Errors.AlreadyDisposed(GetType()));
            ActiveObjects.TryRemove(this, out _);
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (_isEnded)
                return default;

            try {
                if (_reader.TryRead(out var current))
                    _current = current;
                else
                    return MoveNext();
            }
            catch (Exception e) {
                _current = Result.Error<T>(e);
                _isEnded = true;
            }
            return ValueTaskExt.TrueTask;

            async ValueTask<bool> MoveNext()
            {
                try {
                    if (!_isStarted) {
                        _isStarted = true;
                        await _stream.SendAckFromLock(0).ConfigureAwait(false);
                    }
                    if (!await _reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
                        _isEnded = true;
                        return false;
                    }
                    if (!_reader.TryRead(out var current1))
                        throw Errors.InternalError("Couldn't read after successful WaitToReadAsync call.");

                    _current = current1;
                    return true;
                }
                catch (Exception e) {
                    _current = Result.Error<T>(e);
                    _isEnded = true;
                    return true;
                }
            }
        }
    }
}