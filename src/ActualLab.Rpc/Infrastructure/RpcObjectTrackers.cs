using System.Diagnostics.CodeAnalysis;
using ActualLab.Concurrency;
using ActualLab.Internal;
using ActualLab.OS;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcObjectTracker
{
    protected RpcLimits Limits { get; private set; } = null!;

    [field: AllowNull, MaybeNull]
    public RpcPeer Peer {
        get;
        protected set {
            if (field is not null)
                throw Errors.AlreadyInitialized(nameof(Peer));

            field = value;
            Limits = field.Hub.Limits;
        }
    }

    public abstract int Count { get; }

    public virtual void Initialize(RpcPeer peer)
        => Peer = peer;
}

public class RpcRemoteObjectTracker : RpcObjectTracker, IEnumerable<IRpcObject>
{
    private readonly ConcurrentDictionary<long, GCHandle> _objects = new(HardwareInfo.ProcessorCountPo2, 17);

    public override int Count => _objects.Count;

    public IRpcObject? Get(long localId)
        => _objects.TryGetValue(localId, out var handle) && handle.Target is IRpcObject obj
            ? obj
            : null;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<IRpcObject> GetEnumerator()
    {
        foreach (var (_, handle) in _objects) {
            if (handle.Target is IRpcObject obj)
                yield return obj;
        }
    }

    public void Register(IRpcObject obj)
    {
        obj.RequireKind(RpcObjectKind.Remote);
        var id = obj.Id;
        if (id.IsNone)
            throw new ArgumentOutOfRangeException(nameof(obj));

        while (true) {
            GCHandle handle;
            while (_objects.TryGetValue(id.LocalId, out handle) && handle.Target is IRpcObject existingObj) {
                if (ReferenceEquals(obj, existingObj))
                    return; // Already registered

                // Another object with the same id.LocalId is registered,
                // which means we switched to another peer instance (e.g. via LB),
                // and got an object with the same LocalId as we already have.
                // The only reasonable thing here is to remove the old one,
                // which is already unusable at this point.
                existingObj.Disconnect(); // This call must unregister it
            }

            handle = GCHandle.Alloc(obj, GCHandleType.Weak);
            if (_objects.TryAdd(id.LocalId, handle))
                return;
        }
    }

    public bool Unregister(IRpcObject obj)
    {
        obj.RequireKind(RpcObjectKind.Remote);
        var localId = obj.Id.LocalId;
        if (!_objects.TryGetValue(localId, out var handle))
            return false; // Already unregistered or never was
        if (handle.Target is not { } existingObj)
            return false; // Handle target is dead - prob. it was pointing to another object, which died
        if (!ReferenceEquals(obj, existingObj))
            return false; // Another object is registered w/ the same LocalId already
        if (!_objects.TryRemove(localId, handle))
            return false; // Concurrent Unregister won

        handle.Free();
        return true;
    }

    public async Task Maintain(RpcHandshake handshake, CancellationToken cancellationToken)
    {
        try {
            var remotePeerId = handshake.RemotePeerId;
            var reconnectTasks = new List<Task>();
            foreach (var (_, handle) in _objects)
                if (handle.Target is IRpcObject obj) {
                    if (obj.Id.HostId == remotePeerId)
                        reconnectTasks.Add(obj.Reconnect(cancellationToken));
                    else
                        obj.Disconnect();
                }
            await Task.WhenAll(reconnectTasks).ConfigureAwait(false);

            var hub = Peer.Hub;
            var clock = hub.Clock;
            var systemCallSender = hub.SystemCallSender;
            while (true) {
                await clock.Delay(Limits.KeepAlivePeriod, cancellationToken).ConfigureAwait(false);
                var localIds = GetAliveLocalIdsAndReleaseDeadHandles();
                await systemCallSender.KeepAlive(Peer, localIds).ConfigureAwait(false);
            }
        }
        catch {
            // Intended
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public void Disconnect(params long[] localIds)
    {
        foreach (var localId in localIds)
            Get(localId)?.Disconnect();
    }

    public void Abort()
    {
        var objects = _objects.Values.Select(h => h.Target as IRpcObject).ToList();
        _objects.Clear();
        foreach (var obj in objects)
            obj?.Disconnect();
    }

    // Private methods

    private long[] GetAliveLocalIdsAndReleaseDeadHandles()
    {
        var buffer = ArrayBuffer<long>.Lease(false);
        var purgeBuffer = ArrayBuffer<(long, GCHandle)>.Lease(false);
        try {
            foreach (var (id, handle) in _objects) {
                if (handle.Target is IRpcObject)
                    buffer.Add(id);
                else
                    purgeBuffer.Add((id, handle));
            }

            foreach (var (id, handle) in purgeBuffer)
                if (_objects.TryRemove(id, handle))
                    handle.Free();
            return buffer.ToArray();
        }
        finally {
            purgeBuffer.Release();
            buffer.Release();
        }
    }
}

public sealed class RpcSharedObjectTracker : RpcObjectTracker, IEnumerable<IRpcSharedObject>
{
    private long _lastId;
    private long _lastKeepAliveAt; // CpuTimestamp
    private readonly ConcurrentDictionary<long, IRpcSharedObject> _objects = new(HardwareInfo.ProcessorCountPo2, 17);

    public override int Count => _objects.Count;

    public RpcObjectId NextId()
        => new(Peer.Id, Interlocked.Increment(ref _lastId));

    public IRpcSharedObject? Get(long id)
        => _objects.GetValueOrDefault(id);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<IRpcSharedObject> GetEnumerator()
        // ReSharper disable once NotDisposedResourceIsReturned
        => _objects.Values.GetEnumerator();

    public void Register(IRpcSharedObject obj)
    {
        obj.RequireKind(RpcObjectKind.Local);
        var id = obj.Id;
        if (id.IsNone)
            throw new ArgumentOutOfRangeException(nameof(obj));

        if (!_objects.TryAdd(id.LocalId, obj))
            throw Internal.Errors.RpcObjectIsAlreadyUsed();
    }

    public bool Unregister(IRpcSharedObject obj)
    {
        obj.RequireKind(RpcObjectKind.Local);
        return _objects.TryRemove(obj.Id.LocalId, obj);
    }

    public async Task Maintain(RpcHandshake handshake, CancellationToken cancellationToken)
    {
        InterlockedExt.ExchangeIfGreater(ref _lastKeepAliveAt, CpuTimestamp.Now.Value);
        try {
            var hub = Peer.Hub;
            var clock = hub.Clock;
            while (true) {
                await clock.Delay(Limits.ObjectReleasePeriod, cancellationToken).ConfigureAwait(false);
                var keepAliveDelay = CpuTimestamp.Now - new CpuTimestamp(Interlocked.Read(ref _lastKeepAliveAt));
                if (keepAliveDelay > Limits.KeepAliveTimeout) {
                    await Peer.Disconnect(true, Internal.Errors.KeepAliveTimeout()).ConfigureAwait(false);
                    return;
                }
                var minLastKeepAliveAt = CpuTimestamp.Now - Limits.ObjectReleaseTimeout;
                foreach (var (_, obj) in _objects)
                    if (obj.LastKeepAliveAt < minLastKeepAliveAt && Unregister(obj))
                        TryDispose(obj);
            }
        }
        catch {
            // Intended
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public Task KeepAlive(long[] localIds)
    {
        InterlockedExt.ExchangeIfGreater(ref _lastKeepAliveAt, CpuTimestamp.Now.Value);
        var buffer = MemoryBuffer<long>.Lease(false);
        try {
            foreach (var id in localIds) {
                if (Get(id) is { } obj)
                    obj.KeepAlive();
                else
                    buffer.Add(id);
            }
            return buffer.Count > 0
                ? Peer.Hub.SystemCallSender.Disconnect(Peer, buffer.ToArray())
                : Task.CompletedTask;
        }
        finally {
            buffer.Release();
        }
    }

    public async Task Abort(Exception error)
    {
        var abortedIds = new HashSet<long>();
        for (int cycleIndex = 0;; cycleIndex++) {
            var abortedCountBefore = abortedIds.Count;
            foreach (var obj in this)
                if (abortedIds.Add(obj.Id.LocalId))
                    TryDispose(obj);
            var isDisposeHappened = abortedCountBefore != abortedIds.Count;
            if (!isDisposeHappened || cycleIndex >= Limits.ObjectAbortCycleCount)
                break;

            await Task.Delay(Limits.ObjectAbortCyclePeriod).ConfigureAwait(false);
        }
    }

    // Private methods

    public static void TryDispose(IRpcSharedObject obj)
    {
        if (obj is IAsyncDisposable ad)
            _ = ad.DisposeAsync();
        else if (obj is IDisposable d)
            d.Dispose();
    }
}
