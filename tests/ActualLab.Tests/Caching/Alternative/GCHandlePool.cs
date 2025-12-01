using ActualLab.Concurrency;
using ActualLab.Generators;

namespace ActualLab.Tests.Caching.Alternative;

// ReSharper disable once InconsistentNaming
public sealed class GCHandlePool(GCHandlePool.Options settings) : IDisposable
{
    public record Options
    {
        public static readonly Options Default = new();

        public int Capacity { get; init; } = 1024;
        public GCHandleType HandleType { get; init; } = GCHandleType.Weak;
        public int OperationCounterPrecision { get; init; } = StochasticCounter.DefaultPrecision;
    }

    private readonly ConcurrentQueue<GCHandle> _queue = new();
    private StochasticCounter _opCounter = new(settings.OperationCounterPrecision);

    public GCHandleType HandleType { get; } = settings.HandleType;
    public int Capacity { get; } = settings.Capacity;

    public GCHandlePool() : this(Options.Default) { }
    public GCHandlePool(GCHandleType handleType)
        : this(Options.Default with { HandleType = handleType }) { }

#pragma warning disable MA0055
    ~GCHandlePool() => Dispose();
#pragma warning restore MA0055

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GCHandle Acquire(object? target)
        => Acquire(target, RandomShared.Next());

    public GCHandle Acquire(object? target, int random)
    {
        if (_queue.TryDequeue(out var handle)) {
            _opCounter.Decrement(random);
            if (target is not null)
                handle.Target = target;
            return handle;
        }

        _opCounter.Reset();
        return GCHandle.Alloc(target, HandleType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Release(GCHandle handle)
        => Release(handle, RandomShared.Next());

    public bool Release(GCHandle handle, int random)
    {
        if (!_opCounter.TryIncrement(Capacity, random)) {
            handle.Free();
            return false;
        }
        if (!handle.IsAllocated)
            return false;

        handle.Target = null;
        _queue.Enqueue(handle);
        return true;
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out var handle))
            handle.Free();
        _opCounter.Reset();
    }
}
