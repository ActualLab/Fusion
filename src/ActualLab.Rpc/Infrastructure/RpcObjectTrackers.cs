using System.Diagnostics.CodeAnalysis;
using ActualLab.Concurrency;
using ActualLab.Internal;
using ActualLab.OS;
using ActualLab.Pooling;

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
    private readonly ConcurrentDictionary<long, WeakReferenceSlim<IRpcObject>> _storage
        = new(HardwareInfo.ProcessorCountPo2, 17);

    public override int Count => _storage.Count;

#pragma warning disable MA0055
    ~RpcRemoteObjectTracker()
#pragma warning restore MA0055
    {
        // WeakReferenceSlim stores GCHandle, it has to be disposed to release it
        foreach (var (_, weakRef) in _storage) {
            try {
                weakRef.Dispose();
            }
            catch {
                // Intended
            }
        }
    }

    public IRpcObject? Get(long localId)
        => _storage.TryGetValue(localId, out var weakRef) && weakRef.Target is { } target
            ? target
            : null;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<IRpcObject> GetEnumerator()
    {
        foreach (var (_, weakRef) in _storage) {
            if (weakRef.Target is { } target)
                yield return target;
        }
    }

    public void Register(IRpcObject obj)
    {
        // See ComputedRegistry.Register, it's the same logic here

        obj.RequireKind(RpcObjectKind.Remote);
        var id = obj.Id;
        if (id.IsNone)
            throw new ArgumentOutOfRangeException(nameof(obj));


        var spinWait = new SpinWait();
        WeakReferenceSlim<IRpcObject>? newWeakRef = null;
        var ownsNewWeakRef = true;
        try {
            while (true) {
                if (_storage.TryGetValue(id.LocalId, out var weakRef)) {
                    var target = weakRef.Target;
                    if (ReferenceEquals(obj, target))
                        return; // Already registered

                    if (target is not null) {
                        // Another object with the same id.LocalId is registered,
                        // which means we switched to another peer instance (e.g. via LB),
                        // and got an object with the same LocalId as we already have.
                        // The only reasonable thing here is to remove the old one,
                        // which is already unusable at this point.
                        target.Disconnect(); // This call must unregister it
                    }

                    if (_storage.TryRemove(id.LocalId, weakRef))
                        weakRef.Dispose();
                }
                else {
                    newWeakRef ??= new WeakReferenceSlim<IRpcObject>(obj);
                    if (_storage.TryAdd(id.LocalId, newWeakRef)) {
                        ownsNewWeakRef = false;
                        return;
                    }
                }

                spinWait.SpinOnce(); // Safe for WASM
            }
        }
        finally {
            if (ownsNewWeakRef && newWeakRef != null)
                newWeakRef.Dispose();
        }
    }

    public bool Unregister(IRpcObject obj)
    {
        // See ComputedRegistry.Unregister, it's the same logic here

        obj.RequireKind(RpcObjectKind.Remote);
        var localId = obj.Id.LocalId;
        if (!_storage.TryGetValue(localId, out var weakRef))
            return false; // Already unregistered or never was

        var target = weakRef.Target;
        if (!(ReferenceEquals(target, obj) || ReferenceEquals(target, null)))
            return false; // Points to some other object

        // weakRef.Target is null (is gone, i.e. to be pruned)
        // or pointing to the right object
        if (!_storage.TryRemove(localId, weakRef))
            return false;

        weakRef.Dispose();
        return true;

    }

    public async Task Maintain(RpcHandshake handshake, CancellationToken cancellationToken)
    {
        try {
            var remotePeerId = handshake.RemotePeerId;
            var reconnectTasks = new List<Task>();
            foreach (var (_, weakRef) in _storage)
                if (weakRef.Target is { } target) {
                    if (target.Id.HostId == remotePeerId)
                        reconnectTasks.Add(target.Reconnect(cancellationToken));
                    else
                        target.Disconnect();
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
        var objects = _storage.Values.Select(h => h.Target as IRpcObject).ToList();
        _storage.Clear();
        foreach (var obj in objects)
            obj?.Disconnect();
    }

    // Private methods

    private long[] GetAliveLocalIdsAndReleaseDeadHandles()
    {
        var buffer = ArrayBuffer<long>.Lease(false);
        var purgeBuffer = ArrayBuffer<(long, WeakReferenceSlim<IRpcObject>)>.Lease(false);
        try {
            foreach (var (id, weakRef) in _storage) {
                if (weakRef.Target is not null)
                    buffer.Add(id);
                else
                    purgeBuffer.Add((id, weakRef));
            }

            foreach (var (id, weakRef) in purgeBuffer)
                if (_storage.TryRemove(id, weakRef))
                    weakRef.Dispose();
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
                    await Peer
                        .Disconnect(Internal.Errors.KeepAliveTimeout(), cancellationToken)
                        .ConfigureAwait(false);
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
