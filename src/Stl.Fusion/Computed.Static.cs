using System;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion.Internal;

namespace Stl.Fusion
{
    public static class Computed
    {
        private static readonly AsyncLocal<IComputed?> CurrentLocal = new();

        // GetCurrent & ChangeCurrent

        public static IComputed? GetCurrent() => CurrentLocal.Value;

        public static IComputed<T> GetCurrent<T>()
        {
            var untypedCurrent = GetCurrent();
            if (untypedCurrent is IComputed<T> c)
                return c;
            if (untypedCurrent == null)
                throw Errors.ComputedCurrentIsNull();
            throw Errors.ComputedCurrentIsOfIncompatibleType(typeof(IComputed<T>));
        }

        public static ClosedDisposable<IComputed?> ChangeCurrent(IComputed? newCurrent)
        {
            var oldCurrent = GetCurrent();
            if (newCurrent != null)
                ComputeContext.Current.TryCapture(newCurrent);
            if (oldCurrent == newCurrent)
                return Disposable.NewClosed(oldCurrent, _ => { });
            CurrentLocal.Value = newCurrent;
            return Disposable.NewClosed(oldCurrent, oldCurrent1 => CurrentLocal.Value = oldCurrent1);
        }

        public static ClosedDisposable<IComputed?> SuspendDependencyCapture()
            => ChangeCurrent(null);

        // Invalidation

        public static bool IsInvalidating()
            => (ComputeContext.Current.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate;

        public static ComputeContextScope Invalidate()
            => ComputeContext.Invalidate.Activate();
        public static ComputeContextScope SuspendInvalidate()
            => ComputeContext.Default.Activate();

        // TryCapture

        public static async Task<IComputed?> TryCapture(Func<CancellationToken, Task> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed();
            return result;
        }

        public static async Task<IComputed<T>?> TryCapture<T>(Func<CancellationToken, Task<T>> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed<T>? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed<T>();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed<T>();
            return result;
        }

        public static async Task<IComputed?> TryCapture(Func<CancellationToken, ValueTask> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed();
            return result;
        }

        public static async Task<IComputed<T>?> TryCapture<T>(Func<CancellationToken, ValueTask<T>> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed<T>? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed<T>();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed<T>();
            return result;
        }
        
#if NETSTANDARD2_0
        // Capture sync
        public static ComputeContextScope BeginCapture()
        {
            var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            return ccs;
        }
#endif

        // Capture

        public static async Task<IComputed> Capture(Func<CancellationToken, Task> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed();
            if (result == null)
                throw Errors.NoComputedCaptured();
            return result;
        }

        public static async Task<IComputed<T>> Capture<T>(Func<CancellationToken, Task<T>> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed<T>? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed<T>();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed<T>();
            if (result == null)
                throw Errors.NoComputedCaptured();
            return result;
        }

        public static async Task<IComputed> Capture(Func<CancellationToken, ValueTask> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed();
            if (result == null)
                throw Errors.NoComputedCaptured();
            return result;
        }

        public static async Task<IComputed<T>> Capture<T>(Func<CancellationToken, ValueTask<T>> producer, CancellationToken cancellationToken = default)
        {
            using var ccs = ComputeContext.New(CallOptions.Capture).Activate();
            IComputed<T>? result;
            try {
                await producer.Invoke(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                result = ccs.Context.GetCapturedComputed<T>();
                if (result?.Error != null)
                    return result;
                throw;
            }
            result = ccs.Context.GetCapturedComputed<T>();
            if (result == null)
                throw Errors.NoComputedCaptured();
            return result;
        }

        // TryGetExisting

        public static IComputed<T>? TryGetExisting<T>(Func<Task<T>> producer)
        {
            using var ccs = ComputeContext.New(CallOptions.TryGetExisting | CallOptions.Capture).Activate();
            var task = producer.Invoke();
            task.AssertCompleted(); // The must be always synchronous in this case
            return ccs.Context.GetCapturedComputed<T>();
        }

        public static IComputed<T>? TryGetExisting<T>(Func<ValueTask<T>> producer)
        {
            using var ccs = ComputeContext.New(CallOptions.TryGetExisting | CallOptions.Capture).Activate();
            var task = producer.Invoke();
            task.AssertCompleted(); // The must be always synchronous in this case
            return ccs.Context.GetCapturedComputed<T>();
        }
    }
}
