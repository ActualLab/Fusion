using System.Globalization;
using System.Runtime.ExceptionServices;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;
using MessagePack;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

#pragma warning disable MA0055

[DataContract]
public abstract partial class RpcStream : IRpcObject
{
#pragma warning disable CA2201
    // We never throw this error, so it's fine to share its single instance here
    protected static readonly Exception NoMoreItemsTag = new();
#pragma warning restore CA2201
    protected static readonly ConcurrentDictionary<object, Unit> ActiveObjects = new();
    protected static readonly UnboundedChannelOptions RemoteChannelOptions = new() {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false, // We don't want sync handlers to "clog" the call processing loop
    };

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public int AckPeriod { get; init; } = 30;
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public int AckAdvance { get; init; } = 61;

    // Non-serialized members
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public RpcObjectId Id { get; protected set; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public RpcPeer? Peer { get; protected set; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public abstract Type ItemType { get; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public abstract RpcObjectKind Kind { get; }

    public static RpcStream<T> New<T>(IAsyncEnumerable<T> outgoingSource)
        => new(outgoingSource);
    public static RpcStream<T> New<T>(IEnumerable<T> outgoingSource)
        => new(outgoingSource.ToAsyncEnumerable());

    public override string ToString()
        => $"{GetType().GetName()}({Id} @ {Peer?.Ref}, {Kind})";

    Task IRpcObject.Reconnect(CancellationToken cancellationToken)
        => Reconnect(cancellationToken);

    void IRpcObject.Disconnect()
        => Disconnect();

    // Protected methods

    protected internal abstract ArgumentList CreateStreamItemArguments();
    protected internal abstract ArgumentList CreateStreamBatchArguments();
    protected internal abstract Task OnItem(long index, object? item);
    protected internal abstract Task OnBatch(long index, object? items);
    protected internal abstract Task OnEnd(long index, Exception? error);
    protected abstract Task Reconnect(CancellationToken cancellationToken);
    protected abstract void Disconnect();
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[JsonConverter(typeof(RpcStreamJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(RpcStreamNewtonsoftJsonConverter))]
public sealed partial class RpcStream<T> : RpcStream, IAsyncEnumerable<T>
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly IAsyncEnumerable<T>? _localSource;
    private Channel<T>? _remoteChannel;
    private long _nextIndex;
    private bool _isRegistered;
    private bool _isDisconnected;

    [DataMember(Order = 2), MemoryPackOrder(2)]
    public RpcObjectId SerializedId {
        get {
            // This member must never be accessed directly - its only purpose is to be called on serialization
            this.RequireKind(RpcObjectKind.Local);
            lock (_lock) {
                if (!Id.IsNone) // Already registered
                    return Id;

                Peer ??= RpcOutboundContext.Current?.Peer ?? RpcInboundContext.GetCurrent().Peer;
                var sharedObjects = Peer.SharedObjects;
                Id = sharedObjects.NextId(); // NOTE: Id changes on serialization!
                var sharedStream = new RpcSharedStream<T>(this);
                sharedObjects.Register(sharedStream);
                return Id;
            }
        }
        set {
            this.RequireKind(RpcObjectKind.Remote);
            lock (_lock) {
                if (!Id.IsNone) {
                    if (Id == value)
                        return;
                    throw Errors.AlreadyInitialized(nameof(SerializedId));
                }

                Id = value;
                Peer = RpcInboundContext.GetCurrent().Peer;
            }
        }
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreMember]
    public override Type ItemType => typeof(T);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreMember]
    public override RpcObjectKind Kind => _localSource is not null ? RpcObjectKind.Local : RpcObjectKind.Remote;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public RpcStream() { }

    public RpcStream(IAsyncEnumerable<T> localSource)
        => _localSource = localSource;

    ~RpcStream()
    {
        if (_localSource is null)
            Close(Errors.AlreadyDisposed(GetType()));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_localSource is not null)
            return _localSource.GetAsyncEnumerator(cancellationToken);

        lock (_lock) {
            if (_remoteChannel is not null)
                throw Internal.Errors.RemoteRpcStreamCanBeEnumeratedJustOnce();
            if (Peer is null)
                throw Errors.InternalError("RpcStream.Peer is null.");

            _remoteChannel = Channel.CreateUnbounded<T>(RemoteChannelOptions);
            if (_nextIndex == long.MaxValue) // Marked as missing
                _remoteChannel.Writer.TryComplete(Internal.Errors.RpcStreamNotFound());
            else {
                _isRegistered = true;
                Peer.RemoteObjects.Register(this);
                _ = SendResetFromLock(0);
            }
            return new RemoteChannelEnumerator(this, cancellationToken);
        }
    }

    public static string SerializeToString(RpcStream<T>? stream)
    {
        if (stream is null)
            return "";

        using var formatter = ListFormat.CommaSeparated.CreateFormatter();
        var id = stream.SerializedId;
        formatter.Append(id.HostId.ToString());
        formatter.Append(id.LocalId.ToString(CultureInfo.InvariantCulture));
        formatter.Append(stream.AckPeriod.ToString(CultureInfo.InvariantCulture));
        formatter.Append(stream.AckAdvance.ToString(CultureInfo.InvariantCulture));
        formatter.AppendEnd();
        return formatter.Output;
    }

    public static RpcStream<T>? DeserializeFromString(string? source)
    {
        if (source.IsNullOrEmpty())
            return null;

        using var parser = ListFormat.CommaSeparated.CreateParser(source);
        parser.ParseNext();
        var hostId = Guid.Parse(parser.Item);
        parser.ParseNext();
        var localId = long.Parse(parser.Item, CultureInfo.InvariantCulture);
        var id = new RpcObjectId(hostId, localId);
        parser.ParseNext();
        var ackPeriod = int.Parse(parser.Item, CultureInfo.InvariantCulture);
        parser.ParseNext();
        var ackAdvance = int.Parse(parser.Item, CultureInfo.InvariantCulture);
        return new RpcStream<T>() { SerializedId = id, AckPeriod = ackPeriod, AckAdvance = ackAdvance };
    }

    // Protected methods

    internal IAsyncEnumerable<T> GetLocalSource()
    {
        this.RequireKind(RpcObjectKind.Local);
        return _localSource!;
    }

    protected internal override ArgumentList CreateStreamItemArguments()
        => ArgumentList.New<long, T>(0L, default!);

    protected internal override ArgumentList CreateStreamBatchArguments()
        => ArgumentList.New<long, T[]>(0L, default!);

    protected internal override Task OnItem(long index, object? item)
    {
        lock (_lock) {
            if (_remoteChannel is null)
                return Task.CompletedTask;
            if (index < _nextIndex)
                return MaybeSendAckFromLock(index);
            if (index > _nextIndex)
                return SendResetFromLock(_nextIndex);

            // Debug.WriteLine($"{Id}: +#{index} (ack @ {ackIndex})");
            _nextIndex++;
            _remoteChannel.Writer.TryWrite((T)item!); // Must always succeed for unbounded channel
            return Task.CompletedTask;
        }
    }

    protected internal override Task OnBatch(long index, object? items)
    {
        lock (_lock) {
            if (_remoteChannel is null)
                return Task.CompletedTask;
            if (index < _nextIndex)
                return MaybeSendAckFromLock(index);
            if (index > _nextIndex)
                return SendResetFromLock(_nextIndex);

            var typedItems = (T[])items!;
            foreach (var item in typedItems) {
                // Debug.WriteLine($"{Id}: +#{index} (ack @ {ackIndex})");
                _nextIndex++;
                _remoteChannel.Writer.TryWrite(item); // Must always succeed for unbounded channel
            }
            return Task.CompletedTask;
        }
    }

    protected internal override Task OnEnd(long index, Exception? error)
    {
        lock (_lock) {
            if (_remoteChannel is null)
                return Task.CompletedTask;
            if (index < _nextIndex)
                return MaybeSendAckFromLock(index);
            if (index > _nextIndex)
                return SendResetFromLock(_nextIndex);

            // Debug.WriteLine($"{Id}: +{index} (ended!)");
            CloseFromLock(error);
            return Task.CompletedTask;
        }
    }

    protected override Task Reconnect(CancellationToken cancellationToken)
    {
        lock (_lock)
            return _remoteChannel is not null && !_isDisconnected
                ? SendResetFromLock(_nextIndex)
                : Task.CompletedTask;
    }

    protected override void Disconnect()
    {
        lock (_lock) {
            if (_isDisconnected)
                return;

            _isDisconnected = true;
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
        if (_remoteChannel is not null) {
            if (_nextIndex != long.MaxValue)
                _ = SendCloseFromLock();
            _remoteChannel.Writer.TryComplete(error);
        }
        if (_isRegistered) {
            _isRegistered = false;
            Peer?.RemoteObjects.Unregister(this);
        }
    }

    private Task SendCloseFromLock()
    {
        _nextIndex = long.MaxValue;
        return SendAckFromLock(_nextIndex, true);
    }

    private Task SendResetFromLock(long index)
        => SendAckFromLock(index, true);

    private Task MaybeSendAckFromLock(long index)
        => index % AckPeriod == 0 && index > 0
            ? SendAckFromLock(index)
            : Task.CompletedTask;

    private Task SendAckFromLock(long index, bool mustReset = false)
    {
        // Debug.WriteLine($"{Id}: <- ACK: ({index}, {mustReset})");
        if (_isDisconnected)
            return Task.CompletedTask;

        return index != long.MaxValue
            ? Peer!.Hub.SystemCallSender.Ack(Peer, Id.LocalId, index, mustReset ? Id.HostId : default)
            : Peer!.Hub.SystemCallSender.AckEnd(Peer, Id.LocalId, mustReset ? Id.HostId : default);
    }

    // Nested types

    private sealed class RemoteChannelEnumerator : IAsyncEnumerator<T>
    {
        private readonly ChannelReader<T> _reader;
        private long _nextIndex;
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

        public T Current {
            get {
                if (_nextIndex == 0)
                    throw new InvalidOperationException($"{nameof(CompleteMoveNextAsync)} should be called first.");
                if (_current.HasError)
                    throw new InvalidOperationException($"Last {nameof(CompleteMoveNextAsync)} returned false or failed.");

                return _current.ValueOrDefault!;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!ActiveObjects.TryRemove(this, out _))
                return default;

            _stream.Close(Errors.AlreadyDisposed(GetType()));
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            // This method shouldn't throw no matter what,
            // but should return a faulted ValueTask<bool>
            // when the stream terminates with an error.

            if (_current.Error is { } error)
                return GetMoveNextValueTask(error);

            try {
                if (!_reader.TryRead(out var current))
                    return CompleteMoveNextAsync(); // Never throws directly (i.e. only via ValueTask.Result)

                _current = current;
            }
            catch (Exception e) {
                _current = Result.NewError<T>(e);
            }

            var ackTask = _stream.MaybeSendAckFromLock(_nextIndex++);
            return ackTask.IsCompleted
                ? GetMoveNextValueTask(_current.Error)
                : AwaitAndGetMoveNextValueTask(ackTask, _current.Error);
        }

        // Private methods

        private static ValueTask<bool> GetMoveNextValueTask(Exception? error)
            => error is null
                ? ValueTaskExt.TrueTask
                : ReferenceEquals(error, NoMoreItemsTag)
                    ? default
                    : ValueTaskExt.FromException<bool>(error);

        private static async ValueTask<bool> AwaitAndGetMoveNextValueTask(Task taskToAwait, Exception? error)
        {
            await taskToAwait.SilentAwait(false);
            if (error is null)
                return true;

            if (!ReferenceEquals(error, NoMoreItemsTag))
                ExceptionDispatchInfo.Capture(error).Throw(); // Always throws
            return false;
        }

        private async ValueTask<bool> CompleteMoveNextAsync()
        {
            try {
                var ackTask = _stream.MaybeSendAckFromLock(_nextIndex);
                if (!ackTask.IsCompleted)
                    await ackTask.SilentAwait(false);

                if (!await _reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
                    _current = Result.NewError<T>(NoMoreItemsTag);
                    _nextIndex++;
                    return false;
                }

                if (!_reader.TryRead(out var current1))
                    throw Errors.InternalError("Couldn't read after successful WaitToReadAsync call.");

                // await _stream.MaybeSendAckFromLock(_nextIndex).ConfigureAwait(false);
                _current = current1;
                _nextIndex++;
                return true;
            }
            catch (Exception e) {
                _current = Result.NewError<T>(e);
                _nextIndex++;
                throw;
            }
        }
    }
}
