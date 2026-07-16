using ActualLab.Locking;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
public class AsyncLockBenchmarks
{
    private readonly AsyncLock _lock = new();
    private readonly IAsyncLock _interfaceLock;

    public AsyncLockBenchmarks()
        => _interfaceLock = _lock;

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask ConcreteUncontended()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            using var releaser = await _lock.Lock().ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask InterfaceUncontended()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            using var releaser = await _interfaceLock.Lock().ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask OwnerWaiterHandoff()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            var owner = await _lock.Lock().ConfigureAwait(false);
            var waiterTask = _lock.Lock();
            owner.Dispose();
            using var waiter = await waiterTask.ConfigureAwait(false);
        }
    }
}

[MemoryDiagnoser]
public class AsyncLockSetBenchmarks
{
    private readonly AsyncLockSet<long> _lockSet = new();
    private long _nextKey;

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask AbsentKeyUncontended()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            using var releaser = await _lockSet.Lock(_nextKey++).ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask SameKeyUncontended()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            using var releaser = await _lockSet.Lock(0).ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask OwnerWaiterHandoff()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            var owner = await _lockSet.Lock(0).ConfigureAwait(false);
            var waiterTask = _lockSet.Lock(0);
            owner.Dispose();
            using var waiter = await waiterTask.ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask MixedKeysUncontended()
    {
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            using var releaser = await _lockSet.Lock(i & 7).ConfigureAwait(false);
        }
    }
}
