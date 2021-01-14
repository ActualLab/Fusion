using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.DependencyInjection;
using Stl.Fusion.Internal;

namespace Stl.Fusion.Bridge
{
    public interface IReplica : IAsyncDisposableWithDisposalState
    {
        IReplicator Replicator { get; }
        PublicationRef PublicationRef { get; }
        ComputedOptions ComputedOptions { get; set; }
        IReplicaComputed Computed { get; }
        bool IsUpdateRequested { get; }
        Exception? UpdateError { get; }

        Task RequestUpdateAsync(CancellationToken cancellationToken = default);
    }

    public interface IReplica<T> : IReplica
    {
        new IReplicaComputed<T> Computed { get; }
    }

    public interface IReplicaImpl : IReplica, IFunction
    {
        void DisposeTemporaryReplica();
        bool ApplyFailedUpdate(Exception? error, CancellationToken cancellationToken);
    }

    public interface IReplicaImpl<T> : IReplica<T>, IFunction<ReplicaInput, T>, IReplicaImpl
    {
        bool ApplySuccessfulUpdate(Result<T>? output, LTag version, bool isConsistent);
    }

    public class Replica<T> : AsyncDisposableBase, IReplicaImpl<T>
    {
        private volatile ComputedOptions _computedOptions = ComputedOptions.Default;

        protected readonly ReplicaInput Input;
        protected volatile IReplicaComputed<T> ComputedField = null!;
        protected volatile Exception? UpdateErrorField;
        protected volatile Task<Unit>? UpdateRequestTask;
        protected IReplicatorImpl ReplicatorImpl => (IReplicatorImpl) Replicator;
        protected object Lock = new object();

        public IReplicator Replicator { get; }
        public PublicationRef PublicationRef => Input.PublicationRef;
        public ComputedOptions ComputedOptions {
            get => _computedOptions;
            set {
                if (value.SwappingOptions.IsEnabled)
                    throw Errors.UnsupportedComputedOptions(GetType());
                _computedOptions = value;
            }
        }

        public IReplicaComputed<T> Computed => ComputedField;
        public bool IsUpdateRequested => UpdateRequestTask != null;
        public Exception? UpdateError => UpdateErrorField;

        // Explicit property implementations
        IServiceProvider IHasServices.Services => ReplicatorImpl.Services;
        IReplicaComputed IReplica.Computed => ComputedField;

        public Replica(IReplicator replicator, PublicationStateInfo<T> info, bool isUpdateRequested = false)
        {
            Replicator = replicator;
            Input = new ReplicaInput(this, info.PublicationRef);
            // ReSharper disable once VirtualMemberCallInConstructor
            ApplySuccessfulUpdate(info.Output, info.Version, info.IsConsistent);
            if (isUpdateRequested)
                // ReSharper disable once VirtualMemberCallInConstructor
                UpdateRequestTask = CreateUpdateRequestTask();
        }

        // This method is called for temp. replicas that were never attached to anything.
        void IReplicaImpl.DisposeTemporaryReplica()
        {
            if (!MarkDisposed())
                throw Stl.Internal.Errors.InternalError(
                    "Couldn't dispose temporary Replica!");
        }

        // We want to make sure the replicas are connected to
        // publishers only while they're used.
        ~Replica() => DisposeAsync(false);

        protected override ValueTask DisposeInternalAsync(bool disposing)
        {
            Input.ReplicatorImpl.OnReplicaDisposed(this);
            ReplicaRegistry.Instance.Remove(this);
            return ValueTaskEx.CompletedTask;
        }

        Task IReplica.RequestUpdateAsync(CancellationToken cancellationToken)
            => RequestUpdateAsync(cancellationToken);
        public virtual Task RequestUpdateAsync(CancellationToken cancellationToken = default)
        {
            var updateRequestTask = UpdateRequestTask;
            if (updateRequestTask != null)
                return updateRequestTask.WithFakeCancellation(cancellationToken);
            // Double check locking
            lock (Lock) {
                updateRequestTask = UpdateRequestTask;
                if (updateRequestTask != null)
                    return updateRequestTask.WithFakeCancellation(cancellationToken);
                UpdateRequestTask = updateRequestTask = CreateUpdateRequestTask();
                Input.ReplicatorImpl.Subscribe(this);
                return updateRequestTask.WithFakeCancellation(cancellationToken);
            }
        }

        bool IReplicaImpl<T>.ApplySuccessfulUpdate(Result<T>? output, LTag version, bool isConsistent)
            => ApplySuccessfulUpdate(output, version, isConsistent);
        protected virtual bool ApplySuccessfulUpdate(Result<T>? output, LTag version, bool isConsistent)
        {
            Task<Unit>? updateRequestTask;
            lock (Lock) {
                // 1. Update Computed & UpdateError
                UpdateErrorField = null;
                var oldComputed = ComputedField;

                if (oldComputed == null || oldComputed.Version != version)
                    ReplaceComputedUnsafe(oldComputed, output, version, isConsistent);
                else if (oldComputed.IsConsistent() != isConsistent) {
                    if (isConsistent)
                        ReplaceComputedUnsafe(oldComputed, output, version, isConsistent);
                    else
                        oldComputed?.Invalidate();
                }

                // 2. Complete UpdateRequestTask
                (updateRequestTask, UpdateRequestTask) = (UpdateRequestTask, null);
            }

            if (updateRequestTask != null) {
                var updateRequestTaskSource = TaskSource.For(updateRequestTask);
                updateRequestTaskSource.TrySetResult(default);
            }
            return true;
        }

        bool IReplicaImpl.ApplyFailedUpdate(Exception? error, CancellationToken cancellationToken)
            => ApplyFailedUpdate(error, cancellationToken);
        protected virtual bool ApplyFailedUpdate(Exception? error, CancellationToken cancellationToken)
        {
            IReplicaComputed<T>? computed;
            Task<Unit>? updateRequestTask;
            lock (Lock) {
                // 1. Update Computed & UpdateError
                computed = ComputedField;
                UpdateErrorField = error;

                // 2. Complete UpdateRequestTask
                (updateRequestTask, UpdateRequestTask) = (UpdateRequestTask, null);
            }

            if (error != null)
                computed.Invalidate();
            if (updateRequestTask != null) {
                var result = new Result<Unit>(default, error);
                var updateRequestTaskSource = TaskSource.For(updateRequestTask);
                updateRequestTaskSource.TrySetFromResult(result, cancellationToken);
            }
            return true;
        }

        protected virtual Task<Unit> CreateUpdateRequestTask()
            => TaskSource.New<Unit>(true).Task;

        protected virtual void ReplaceComputedUnsafe(
            IReplicaComputed<T>? oldComputed,
            Result<T>? output, LTag version, bool isConsistent)
        {
            oldComputed?.Invalidate();
            if (output.HasValue) {
                var newComputed = new ReplicaComputed<T>(
                    ComputedOptions, Input, output.GetValueOrDefault(), version, isConsistent);
                if (isConsistent)
                    ComputedRegistry.Instance.Register(newComputed);
                ComputedField = newComputed;
            }
        }

        protected async Task<IComputed<T>> InvokeAsync(
            ReplicaInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
        {
            if (input != Input)
                // This "Function" supports just a single input == Input
                throw new ArgumentOutOfRangeException(nameof(input));

            context ??= ComputeContext.Current;

            var result = Computed;
            if (result.TryUseExisting(context, usedBy))
                return result;

            // No async locking here b/c RequestUpdateAsync is, in fact, doing this
            await RequestUpdateAsync(cancellationToken).ConfigureAwait(false);
            result = Computed;
            result.UseNew(context, usedBy);
            return result;
        }

        protected async Task<T> InvokeAndStripAsync(
            ReplicaInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
        {
            if (input != Input)
                // This "Function" supports just a single input == Input
                throw new ArgumentOutOfRangeException(nameof(input));

            context ??= ComputeContext.Current;

            var result = Computed;
            if (result.TryUseExisting(context, usedBy))
                return result.Strip();

            // No async locking here b/c RequestUpdateAsync is, in fact, doing this
            await RequestUpdateAsync(cancellationToken).ConfigureAwait(false);
            result = Computed;
            result.UseNew(context, usedBy);
            return result.Value;
        }

        #region Explicit impl. of IFunction & IFunction<...>

        async Task<IComputed> IFunction.InvokeAsync(ComputedInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
            => await InvokeAsync((ReplicaInput) input, usedBy, context, cancellationToken);

        Task IFunction.InvokeAndStripAsync(ComputedInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
            => InvokeAndStripAsync((ReplicaInput) input, usedBy, context, cancellationToken);

        Task<IComputed<T>> IFunction<ReplicaInput, T>.InvokeAsync(ReplicaInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
            => InvokeAsync(input, usedBy, context, cancellationToken);

        Task<T> IFunction<ReplicaInput, T>.InvokeAndStripAsync(ReplicaInput input, IComputed? usedBy, ComputeContext? context,
            CancellationToken cancellationToken)
            => InvokeAndStripAsync(input, usedBy, context, cancellationToken);

        #endregion
    }
}
