using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion.Bridge.Internal;
using Stl.Text;
using Stl.Time;
using Errors = Stl.Internal.Errors;

namespace Stl.Fusion.Bridge
{
    public interface IPublication : IAsyncProcess
    {
        IPublisher Publisher { get; }
        Symbol Id { get; }
        PublicationRef Ref { get; }
        IPublicationState State { get; }
        long UseCount { get; }

        Type GetResultType();
        Disposable<IPublication> Use();
        bool Touch();
        ValueTask UpdateAsync(CancellationToken cancellationToken);

        // Convenience helpers
        TResult Apply<TArg, TResult>(IPublicationApplyHandler<TArg, TResult> handler, TArg arg);
    }

    public interface IPublication<T> : IPublication
    {
        new IPublicationState<T> State { get; }
    }

    public class Publication<T> : AsyncProcessBase, IPublication<T>
    {
        private long _lastTouchTime;
        private long _useCount;

        protected IMomentClock Clock { get; }
        protected volatile IPublicationStateImpl<T> StateField;
        protected IPublisherImpl PublisherImpl => (IPublisherImpl) Publisher;
        protected Moment LastTouchTime {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Moment(Volatile.Read(ref _lastTouchTime));
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Volatile.Write(ref _lastTouchTime, value.EpochOffset.Ticks);
        }

        // Properties
        public Type PublicationType { get; }
        public IPublisher Publisher { get; }
        public Symbol Id { get; }
        public PublicationRef Ref => new(Publisher.Id, Id);
        IPublicationState IPublication.State => State;
        public IPublicationState<T> State => StateField;
        public long UseCount => Volatile.Read(ref _useCount);

        public Publication(
            Type publicationType, IPublisher publisher,
            IComputed<T> computed, Symbol id,
            IMomentClock? clock)
        {
            Clock = clock ??= CoarseCpuClock.Instance;
            PublicationType = publicationType;
            Publisher = publisher;
            Id = id;
            LastTouchTime = clock.Now;
            StateField = CreatePublicationState(computed);
        }

        public Type GetResultType()
            => typeof(T);

        public bool Touch()
        {
            if (State.IsDisposed)
                return false;
            LastTouchTime = CoarseCpuClock.Now;
            return true;
        }

        public Disposable<IPublication> Use()
        {
            if (State.IsDisposed)
                throw Errors.AlreadyDisposedOrDisposing();
            Interlocked.Increment(ref _useCount);
            return new Disposable<IPublication>(this, p => {
                var self = (Publication<T>) p;
                if (0 == Interlocked.Decrement(ref self._useCount))
                    Touch();
            });
        }

        public async ValueTask UpdateAsync(CancellationToken cancellationToken)
        {
            var state = StateField;
            if (state.IsDisposed || state.Computed.IsConsistent())
                return;
            var newComputed = await state.Computed
                .UpdateAsync(false, cancellationToken).ConfigureAwait(false);
            var newState = CreatePublicationState(newComputed);
            ChangeState(newState, state);
        }

        public TResult Apply<TArg, TResult>(IPublicationApplyHandler<TArg, TResult> handler, TArg arg)
            => handler.Apply(this, arg);

        // Protected methods

        protected virtual IPublicationStateImpl<T> CreatePublicationState(
            IComputed<T> computed, bool isDisposed = false)
            => new PublicationState<T>(this, computed, Clock.Now, isDisposed);

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            try {
                await ExpireAsync(cancellationToken).ConfigureAwait(false);
            }
            finally {
                // Awaiting for disposal here = cyclic task dependency;
                // we should just ensure it starts right when this method
                // completes.
                var _ = DisposeAsync();
            }
        }

        protected virtual async Task ExpireAsync(CancellationToken cancellationToken)
        {
            var expirationTime = PublisherImpl.PublicationExpirationTime;

            Moment GetLastUseTime()
                => UseCount > 0 ? CoarseCpuClock.Now : LastTouchTime;
            Moment GetNextCheckTime(Moment start, Moment lastUseTime)
                => lastUseTime + expirationTime;

            // Uncomment for debugging:
            // await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);

            try {
                var start = CoarseCpuClock.Now;
                var lastUseTime = GetLastUseTime();
                while (true) {
                    var nextCheckTime = GetNextCheckTime(start, lastUseTime);
                    var delay = TimeSpan.FromTicks(Math.Max(0, (nextCheckTime - Clock.Now).Ticks));
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    var newLastUseTime = GetLastUseTime();
                    if (newLastUseTime == lastUseTime)
                        break;
                    lastUseTime = newLastUseTime;
                }
            }
            catch (OperationCanceledException) {
                // RunExpirationAsync is called w/o Task.Run, so there is a chance
                // it throws this exception synchronously (if cts got cancelled already).
            }
        }

        protected override Task DisposeAsync(bool disposing)
        {
            // We override this method to make sure State is the first thing
            // to reflect the disposal.
            var state = StateField;
            if (state.IsDisposed)
                return Task.CompletedTask;
            var newState = CreatePublicationState(state.Computed, true);
            if (!ChangeState(newState, state))
                return Task.CompletedTask;
            return base.DisposeAsync(disposing);
        }

        protected override async ValueTask DisposeInternalAsync(bool disposing)
        {
            await base.DisposeInternalAsync(disposing).ConfigureAwait(false);
            if (Publisher is IPublisherImpl pi)
                pi.OnPublicationDisposed(this);
        }

        // State change & other low-level stuff

        protected bool ChangeState(IPublicationStateImpl<T> newState, IPublicationStateImpl<T> expectedState)
        {
            var oldState = Interlocked.CompareExchange(ref StateField, newState, expectedState);
            if (oldState != expectedState)
                return false;
            expectedState.TryMarkOutdated();
            return true;
        }
    }
}
